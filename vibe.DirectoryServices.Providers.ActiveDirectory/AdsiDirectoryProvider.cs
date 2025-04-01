using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;

namespace vibe.DirectoryServices.Providers.Adsi
{
    public abstract class AdsiDirectoryProvider : IAdsiDirectoryProvider
    {
        protected readonly Dictionary<string, GroupPrincipal> _groupPrincipalCache =
            new Dictionary<string, GroupPrincipal>();

        protected PrincipalContext _context;

        protected AdsiDirectoryProvider()
        {
            _context = GetContext();
        }

        public abstract string ProviderId { get; }
        public virtual bool SupportsOperation => true;

        public virtual IDirectoryUser<string> CreateUser(DirectoryUserCreationParams parameters)
        {
            var userPrincipal = new UserPrincipal(_context)
            {
                SamAccountName = parameters.Username,
                DisplayName = parameters.DisplayName,
                EmailAddress = parameters.Email
            };
            userPrincipal.Save();

            return CreateUserFromPrincipal(userPrincipal);
        }

        public virtual IDirectoryGroup<string> CreateGroup(DirectoryGroupCreationParams parameters)
        {
            var groupPrincipal = new GroupPrincipal(_context)
            {
                Name = parameters.GroupName,
                Description = parameters.Description
            };
            groupPrincipal.Save();

            var group = CreateGroupFromPrincipal(groupPrincipal);
            _groupPrincipalCache[groupPrincipal.Sid.ToString()] = groupPrincipal;

            return group;
        }

        public virtual IDirectoryUser<string> FindUser(string username)
        {
            var userPrincipal = UserPrincipal.FindByIdentity(_context, username);

            if (userPrincipal == null)
                return null;

            return CreateUserFromPrincipal(userPrincipal);
        }

        public virtual IDirectoryGroup<string> FindGroup(string groupName)
        {
            var groupPrincipal = GroupPrincipal.FindByIdentity(_context, groupName);

            if (groupPrincipal == null)
                return null;

            var group = CreateGroupFromPrincipal(groupPrincipal);
            _groupPrincipalCache[groupPrincipal.Sid.ToString()] = groupPrincipal;

            return group;
        }

        public virtual void AddMemberToGroup(IDirectoryGroup<string> group, IDirectoryEntity<string> member)
        {
            if (group.ProviderId != ProviderId)
                throw new ArgumentException($"Group is not from the {ProviderId} provider");

            var groupPrincipal = GetGroupPrincipal(group);

            if (member.ProviderId == ProviderId)
            {
                // Same provider - use direct approach
                var memberPrincipal = ResolvePrincipal(member);
                if (memberPrincipal != null)
                {
                    groupPrincipal.Members.Add(memberPrincipal);
                    groupPrincipal.Save();
                }
            }
            else
            {
                // Cross-provider case
                if (CanHandleForeignEntity(member))
                    AddCrossProviderMember(groupPrincipal, member);
                else
                    throw new NotSupportedException(
                        $"Provider {ProviderId} cannot handle entities from provider {member.ProviderId}");
            }
        }

        public virtual void RemoveMemberFromGroup(IDirectoryGroup<string> group, IDirectoryEntity<string> member)
        {
            if (group.ProviderId != ProviderId)
                throw new ArgumentException($"Group is not from the {ProviderId} provider");

            var groupPrincipal = GetGroupPrincipal(group);

            if (member.ProviderId == ProviderId)
            {
                // Same provider - use direct approach
                var principal = ResolvePrincipal(member);
                if (principal != null)
                {
                    groupPrincipal.Members.Remove(principal);
                    groupPrincipal.Save();
                }
            }
            else
            {
                // Cross-provider case
                if (CanHandleForeignEntity(member))
                {
                    if (!RemoveCrossProviderMember(groupPrincipal, member))
                        throw new InvalidOperationException(
                            $"Failed to remove member from {member.ProviderId} provider in {ProviderId} group");
                }
                else
                {
                    throw new NotSupportedException(
                        $"Provider {ProviderId} cannot handle entities from provider {member.ProviderId}");
                }
            }
        }

        public virtual IEnumerable<IDirectoryEntity<string>> GetGroupMembers(IDirectoryGroup<string> group)
        {
            if (group.ProviderId != ProviderId)
                throw new ArgumentException($"Group is not from the {ProviderId} provider");

            var groupPrincipal = GetGroupPrincipal(group);
            var members = new List<IDirectoryEntity<string>>();

            foreach (var member in groupPrincipal.Members)
                if (member is UserPrincipal userPrincipal && IsFromCurrentContext(userPrincipal))
                    members.Add(CreateUserFromPrincipal(userPrincipal));
                else if (member is GroupPrincipal nestedGroupPrincipal && IsFromCurrentContext(nestedGroupPrincipal))
                    members.Add(CreateGroupFromPrincipal(nestedGroupPrincipal));

            return members;
        }

        public virtual bool IsGroupMember(IDirectoryGroup<string> group, IDirectoryEntity<string> entity)
        {
            if (group.ProviderId != ProviderId)
                throw new ArgumentException($"Group is not from the {ProviderId} provider");

            var groupPrincipal = GetGroupPrincipal(group);

            var isDirectMember = groupPrincipal.Members
                .OfType<Principal>()
                .Any(p => p.Sid.ToString() == entity.Sid);

            if (isDirectMember)
                return true;

            foreach (var member in groupPrincipal.Members)
                if (member is GroupPrincipal nestedGroup && IsFromCurrentContext(nestedGroup))
                {
                    var nestedDirectoryGroup = CreateGroupFromPrincipal(nestedGroup);

                    if (IsGroupMember(nestedDirectoryGroup, entity))
                        return true;
                }

            return false;
        }

        public virtual bool SupportsSidLookup(string sid)
        {
            // ADSI providers can handle SIDs in string format
            return sid != null && (sid.StartsWith("S-1-") || sid.StartsWith("S-2-"));
        }

        public virtual IDirectoryUser<string> GetUserBySid(string sid)
        {
            if (!SupportsSidLookup(sid))
                return null;

            var userPrincipal = UserPrincipal.FindByIdentity(_context, IdentityType.Sid, sid);

            if (userPrincipal == null)
                return null;

            return CreateUserFromPrincipal(userPrincipal);
        }

        public virtual IDirectoryGroup<string> GetGroupBySid(string sid)
        {
            if (!SupportsSidLookup(sid))
                return null;

            if (_groupPrincipalCache.TryGetValue(sid, out var cachedGroupPrincipal))
                return CreateGroupFromPrincipal(cachedGroupPrincipal);

            var groupPrincipal = GroupPrincipal.FindByIdentity(_context, IdentityType.Sid, sid);

            if (groupPrincipal == null)
                return null;

            var group = CreateGroupFromPrincipal(groupPrincipal);
            _groupPrincipalCache[groupPrincipal.Sid.ToString()] = groupPrincipal;

            return group;
        }

        public virtual bool CanHandleForeignEntity(IDirectoryEntity<string> entity)
        {
            // By default, each provider only handles its own entities
            // Override in concrete providers to handle specific cross-provider scenarios
            return entity.ProviderId == ProviderId;
        }

        public virtual Principal ResolvePrincipal(IDirectoryEntity<string> entity)
        {
            // Default implementation for ADSI providers
            if (entity.ProviderId == ProviderId)
            {
                // Same provider - use direct SID lookup
                if (entity is IDirectoryUser<string>)
                    return UserPrincipal.FindByIdentity(_context, IdentityType.Sid, entity.Sid);
                if (entity is IDirectoryGroup<string>)
                    return GroupPrincipal.FindByIdentity(_context, IdentityType.Sid, entity.Sid);
            }

            // For foreign providers, attempt SID resolution if possible
            try
            {
                var sid = new SecurityIdentifier(entity.Sid);
                return Principal.FindByIdentity(_context, IdentityType.Sid, sid.Value);
            }
            catch
            {
                return null;
            }
        }

        protected abstract PrincipalContext GetContext();

        protected abstract void AddCrossProviderMember(GroupPrincipal groupPrincipal, IDirectoryEntity<string> member);

        protected abstract bool RemoveCrossProviderMember(GroupPrincipal groupPrincipal,
            IDirectoryEntity<string> member);

        protected abstract IDirectoryUser<string> CreateUserFromPrincipal(UserPrincipal userPrincipal);
        protected abstract IDirectoryGroup<string> CreateGroupFromPrincipal(GroupPrincipal groupPrincipal);

        protected virtual GroupPrincipal GetGroupPrincipal(IDirectoryGroup<string> group)
        {
            if (group.ProviderId != ProviderId)
                throw new ArgumentException($"Group is not from the {ProviderId} provider");

            if (_groupPrincipalCache.TryGetValue(group.Sid, out var cachedGroup))
                return cachedGroup;

            var groupPrincipal = GroupPrincipal.FindByIdentity(_context, IdentityType.Sid, group.Sid);
            if (groupPrincipal == null)
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found");

            _groupPrincipalCache[group.Sid] = groupPrincipal;
            return groupPrincipal;
        }

        protected virtual bool IsFromCurrentContext(Principal principal)
        {
            return principal.Context.ContextType == _context.ContextType;
        }
    }
}