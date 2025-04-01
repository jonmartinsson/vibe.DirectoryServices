using Microsoft.Extensions.Logging;
using System;
using vibe.DirectoryServices;

namespace ConsoleTester.Runners.LdapNet
{
    public class LdapDemoRunner
    {
        private readonly ILogger<LdapDemoRunner> _logger;

        public LdapDemoRunner(ILogger<LdapDemoRunner> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(DirectoryServiceFactory factory)
        {
            try
            {
                IDirectoryProvider<string> ldapProvider = factory.GetProvider("LdapNet");

                Console.WriteLine("\nRunning LDAP.NET provider demo...");
                _logger.LogInformation("Starting LDAP.NET provider demo");

                // Create test user
                Console.WriteLine("Creating test user...");
                IDirectoryUser<string> ldapUser = ldapProvider.CreateUser(new DirectoryUserCreationParams
                {
                    Username = "testuser", DisplayName = "Test User", Email = "test.user@example.com"
                });

                Console.WriteLine($"Created user: {ldapUser.DisplayName} with SID: {ldapUser.Sid}");
                _logger.LogDebug("Created LDAP user: {Username}, {Sid}", ldapUser.DisplayName, ldapUser.Sid);

                // Create test group
                Console.WriteLine("Creating test group...");
                IDirectoryGroup<string> ldapGroup = ldapProvider.CreateGroup(new DirectoryGroupCreationParams
                {
                    GroupName = "TestGroup", Description = "Test Group Description"
                });

                Console.WriteLine($"Created group: {ldapGroup.GroupName} with SID: {ldapGroup.Sid}");
                _logger.LogDebug("Created LDAP group: {GroupName}, {Sid}", ldapGroup.GroupName, ldapGroup.Sid);

                // Add user to group
                Console.WriteLine("Adding user to group...");
                ldapGroup.AddMember(ldapUser);
                _logger.LogDebug("Added {User} to {Group}", ldapUser.DisplayName, ldapGroup.GroupName);

                // Verify membership
                Console.WriteLine(
                    $"Is {ldapUser.DisplayName} in {ldapGroup.GroupName}? {ldapGroup.IsMember(ldapUser)}");

                // List group members
                Console.WriteLine($"Members of {ldapGroup.GroupName}:");
                foreach (IDirectoryEntity<string> member in ldapGroup.GetMembers())
                {
                    if (member is IDirectoryUser<string> u)
                    {
                        Console.WriteLine($"  User: {u.DisplayName}");
                        _logger.LogDebug("Found user member: {User}", u.DisplayName);
                    }
                    else if (member is IDirectoryGroup<string> g)
                    {
                        Console.WriteLine($"  Group: {g.GroupName}");
                        _logger.LogDebug("Found group member: {Group}", g.GroupName);
                    }
                }

                _logger.LogInformation("Completed LDAP.NET provider demo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LDAP.NET demo");
                Console.WriteLine($"Error in LDAP.NET demo: {ex.Message}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    _logger.LogError(ex.InnerException, "Inner exception in LDAP.NET demo");
                }
            }
        }
    }
}