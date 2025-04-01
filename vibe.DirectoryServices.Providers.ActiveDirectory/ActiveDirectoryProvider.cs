using System;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace vibe.DirectoryServices.Providers.Adsi
{
    public class ActiveDirectoryProvider : AdsiDirectoryProvider
    {
        public override string ProviderId => "ActiveDirectory";

        protected override PrincipalContext GetContext()
        {
            return new PrincipalContext(ContextType.Domain);
        }

        protected override IDirectoryUser<string> CreateUserFromPrincipal(UserPrincipal userPrincipal)
        {
            return new ActiveDirectoryUser(
                userPrincipal.Sid.ToString(),
                userPrincipal.SamAccountName,
                userPrincipal.DisplayName,
                userPrincipal.DistinguishedName
            );
        }

        protected override IDirectoryGroup<string> CreateGroupFromPrincipal(GroupPrincipal groupPrincipal)
        {
            return new ActiveDirectoryGroup(
                groupPrincipal.Sid.ToString(),
                groupPrincipal.Name,
                groupPrincipal.DistinguishedName,
                this
            );
        }

        protected override void AddCrossProviderMember(GroupPrincipal groupPrincipal, IDirectoryEntity<string> member)
        {
            if (!CanHandleForeignEntity(member))
                throw new NotSupportedException($"Cannot add {member.ProviderId} entities to Active Directory groups.");

            var principal = ResolvePrincipal(member);
            if (principal == null)
                throw new InvalidOperationException($"Could not resolve {member.ProviderId} entity into a principal.");

            try
            {
                groupPrincipal.Members.Add(principal);
                groupPrincipal.Save();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error adding entity to AD group: {ex.Message}", ex);
            }
        }

        protected override bool RemoveCrossProviderMember(GroupPrincipal groupPrincipal,
            IDirectoryEntity<string> member)
        {
            if (member.ProviderId == "WindowsLocal")
                try
                {
                    // For Windows local users/groups, try to find the foreign security principal
                    var principal = ResolvePrincipal(member);
                    if (principal != null)
                    {
                        groupPrincipal.Members.Remove(principal);
                        groupPrincipal.Save();
                        return true;
                    }

                    // Check for foreign security principals - would be implementation specific
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }

            return false;
        }

        public override bool CanHandleForeignEntity(IDirectoryEntity<string> entity)
        {
            // Active Directory can handle Windows local accounts through foreign security principals
            return entity.ProviderId == ProviderId || entity.ProviderId == "WindowsLocal";
        }

        public override Principal ResolvePrincipal(IDirectoryEntity<string> entity)
        {
            // First try the base implementation
            var principal = base.ResolvePrincipal(entity);
            if (principal != null)
                return principal;

            // Handle Windows local accounts specifically
            if (entity.ProviderId == "WindowsLocal")
                try
                {
                    var sid = new SecurityIdentifier(entity.Sid);
                    var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));

                    return Principal.FindByIdentity(_context, IdentityType.SamAccountName, ntAccount.Value);
                }
                catch
                {
                    return null;
                }

            return null;
        }
    }
}