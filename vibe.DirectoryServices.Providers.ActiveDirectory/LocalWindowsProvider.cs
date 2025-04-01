using System;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace vibe.DirectoryServices.Providers.Adsi
{
    public class LocalWindowsProvider : AdsiDirectoryProvider
    {
        public override string ProviderId => "WindowsLocal";

        protected override PrincipalContext GetContext()
        {
            return new PrincipalContext(ContextType.Machine);
        }

        protected override IDirectoryUser<string> CreateUserFromPrincipal(UserPrincipal userPrincipal)
        {
            return new LocalWindowsUser(
                userPrincipal.Sid.ToString(),
                userPrincipal.SamAccountName,
                userPrincipal.DisplayName,
                $"{Environment.MachineName}\\{userPrincipal.SamAccountName}"
            );
        }

        protected override IDirectoryGroup<string> CreateGroupFromPrincipal(GroupPrincipal groupPrincipal)
        {
            return new LocalWindowsGroup(
                groupPrincipal.Sid.ToString(),
                groupPrincipal.Name,
                $"{Environment.MachineName}\\{groupPrincipal.Name}",
                this
            );
        }

        protected override void AddCrossProviderMember(GroupPrincipal groupPrincipal, IDirectoryEntity<string> member)
        {
            if (!CanHandleForeignEntity(member))
                throw new NotSupportedException($"Cannot add {member.ProviderId} entities to Windows local groups.");

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
                throw new InvalidOperationException($"Error adding entity to Windows local group: {ex.Message}", ex);
            }
        }

        protected override bool RemoveCrossProviderMember(GroupPrincipal groupPrincipal,
            IDirectoryEntity<string> member)
        {
            if (!CanHandleForeignEntity(member))
                return false;

            try
            {
                var principal = ResolvePrincipal(member);
                if (principal != null)
                {
                    groupPrincipal.Members.Remove(principal);
                    groupPrincipal.Save();
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool CanHandleForeignEntity(IDirectoryEntity<string> entity)
        {
            // Windows Local can handle Active Directory accounts
            return entity.ProviderId == ProviderId || entity.ProviderId == "ActiveDirectory";
        }

        public override Principal ResolvePrincipal(IDirectoryEntity<string> entity)
        {
            // First try the base implementation
            var principal = base.ResolvePrincipal(entity);
            if (principal != null)
                return principal;

            // Handle Active Directory accounts specifically
            if (entity.ProviderId == "ActiveDirectory")
                try
                {
                    var sid = new SecurityIdentifier(entity.Sid);
                    var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));

                    // Try local context first
                    principal = Principal.FindByIdentity(_context, IdentityType.SamAccountName, ntAccount.Value);
                    if (principal != null)
                        return principal;

                    // Try with domain context if not found locally
                    using (var domainContext = new PrincipalContext(ContextType.Domain))
                    {
                        return Principal.FindByIdentity(domainContext, ntAccount.Value);
                    }
                }
                catch
                {
                    return null;
                }

            return null;
        }
    }
}