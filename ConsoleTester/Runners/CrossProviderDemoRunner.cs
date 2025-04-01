using Microsoft.Extensions.Logging;
using System;
using vibe.DirectoryServices;

namespace ConsoleTester.Runners
{
    public class CrossProviderDemoRunner
    {
        private readonly IDirectoryService<string> _directoryService;
        private readonly ILogger<CrossProviderDemoRunner> _logger;

        public CrossProviderDemoRunner(
            ILogger<CrossProviderDemoRunner> logger,
            IDirectoryService<string> directoryService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        }

        public void Run(DirectoryServiceFactory factory, ProviderSelections selections)
        {
            Console.WriteLine("\nDemonstrating cross-provider functionality:");
            _logger.LogInformation("Starting cross-provider demo");

            try
            {
                if (selections.UseJson && (selections.UseWindowsLocal || selections.UseActiveDirectory))
                {
                    DemoJsonWithWindows(factory);
                }

                if (selections.UseJson && selections.UseLdapNet)
                {
                    DemoJsonWithLdap(factory);
                }

                if ((selections.UseWindowsLocal || selections.UseActiveDirectory) && selections.UseLdapNet)
                {
                    DemoWindowsWithLdap(factory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cross-provider demo");
                Console.WriteLine($"Cross-provider demo error: {ex.Message}");
            }
        }

        private void DemoJsonWithWindows(DirectoryServiceFactory factory)
        {
            try
            {
                IDirectoryProvider<string> jsonProvider = factory.GetProvider("JsonFile");

                _logger.LogInformation("Starting JSON-Windows cross-provider demo");

                // Find a user from Windows Local or Active Directory
                IDirectoryUser<string> windowsUser = null;
                foreach (IDirectoryUser<string> user in _directoryService.SearchUsers("admin", UserSearchType.Username))
                {
                    if (user.ProviderId == "WindowsLocal" || user.ProviderId == "ActiveDirectory")
                    {
                        windowsUser = user;
                        break;
                    }
                }

                if (windowsUser != null)
                {
                    // Create a group in JSON provider
                    IDirectoryGroup<string> crossProviderGroup = jsonProvider.CreateGroup(
                        new DirectoryGroupCreationParams
                        {
                            GroupName = "CrossProviderGroup",
                            Description = "Group with members from different providers"
                        });

                    _logger.LogDebug("Created JSON group for cross-provider demo: {GroupName}",
                        crossProviderGroup.GroupName);

                    // Add the Windows user to the JSON group
                    try
                    {
                        crossProviderGroup.AddMember(windowsUser);
                        Console.WriteLine(
                            $"Added {windowsUser.DisplayName} ({windowsUser.ProviderId}) to {crossProviderGroup.GroupName} ({crossProviderGroup.ProviderId})");
                        Console.WriteLine(
                            $"Is {windowsUser.DisplayName} in {crossProviderGroup.GroupName}? {crossProviderGroup.IsMember(windowsUser)}");

                        _logger.LogDebug("Successfully added Windows user {User} to JSON group {Group}",
                            windowsUser.DisplayName, crossProviderGroup.GroupName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Cross-provider operation failed: {ex.Message}");
                        _logger.LogError(ex, "Error adding Windows user to JSON group");
                    }
                }
                else
                {
                    Console.WriteLine("No Windows or AD users found for cross-provider demo");
                    _logger.LogWarning("No Windows users found for cross-provider demo");
                }

                _logger.LogInformation("Completed JSON-Windows cross-provider demo");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON-Windows cross-provider demo failed: {ex.Message}");
                _logger.LogError(ex, "Error in JSON-Windows cross-provider demo");
            }
        }

        private void DemoJsonWithLdap(DirectoryServiceFactory factory)
        {
            try
            {
                IDirectoryProvider<string> jsonProvider = factory.GetProvider("JsonFile");
                IDirectoryProvider<string> ldapProvider = factory.GetProvider("LdapNet");

                _logger.LogInformation("Starting JSON-LDAP cross-provider demo");

                // Create a user in JSON provider
                IDirectoryUser<string> jsonUser = jsonProvider.CreateUser(new DirectoryUserCreationParams
                {
                    Username = "crossuser", DisplayName = "Cross Provider User", Email = "cross.user@example.com"
                });

                _logger.LogDebug("Created JSON user for cross-provider demo: {User}", jsonUser.DisplayName);

                // Create a group in LDAP provider
                IDirectoryGroup<string> ldapGroup = ldapProvider.CreateGroup(new DirectoryGroupCreationParams
                {
                    GroupName = "CrossGroup", Description = "Group for cross-provider testing"
                });

                _logger.LogDebug("Created LDAP group for cross-provider demo: {Group}", ldapGroup.GroupName);

                // Try to add JSON user to LDAP group
                try
                {
                    ldapGroup.AddMember(jsonUser);
                    Console.WriteLine(
                        $"Added {jsonUser.DisplayName} ({jsonUser.ProviderId}) to {ldapGroup.GroupName} ({ldapGroup.ProviderId})");
                    _logger.LogDebug("Successfully added JSON user to LDAP group");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cross-provider operation failed: {ex.Message}");
                    Console.WriteLine("This is expected as LDAP requires mapping between directory entries");
                    _logger.LogInformation("Expected cross-provider limitation: {Error}", ex.Message);
                }

                _logger.LogInformation("Completed JSON-LDAP cross-provider demo");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON-LDAP cross-provider demo failed: {ex.Message}");
                _logger.LogError(ex, "Error in JSON-LDAP cross-provider demo");
            }
        }

        private void DemoWindowsWithLdap(DirectoryServiceFactory factory)
        {
            try
            {
                _logger.LogInformation("Starting Windows-LDAP cross-provider demo");

                // Find a Windows or AD user
                IDirectoryUser<string> windowsUser = null;
                foreach (IDirectoryUser<string> user in _directoryService.SearchUsers("admin", UserSearchType.Username))
                {
                    if (user.ProviderId == "WindowsLocal" || user.ProviderId == "ActiveDirectory")
                    {
                        windowsUser = user;
                        break;
                    }
                }

                if (windowsUser == null)
                {
                    Console.WriteLine("No Windows users found for cross-provider demo with LDAP");
                    _logger.LogWarning("No Windows users found for Windows-LDAP cross-provider demo");
                    return;
                }

                // Get the LDAP provider
                IDirectoryProvider<string> ldapProvider = factory.GetProvider("LdapNet");

                // Create a group in LDAP
                IDirectoryGroup<string> ldapGroup = ldapProvider.CreateGroup(new DirectoryGroupCreationParams
                {
                    GroupName = "WindowsIntegrationGroup", Description = "Group for Windows-LDAP integration"
                });

                _logger.LogDebug("Created LDAP group for Windows integration: {Group}", ldapGroup.GroupName);

                // Try to add Windows user to LDAP group
                try
                {
                    ldapGroup.AddMember(windowsUser);
                    Console.WriteLine(
                        $"Added {windowsUser.DisplayName} ({windowsUser.ProviderId}) to {ldapGroup.GroupName} (LdapNet)");
                    _logger.LogDebug("Successfully added Windows user to LDAP group");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Windows-LDAP cross-provider operation failed: {ex.Message}");
                    Console.WriteLine(
                        "This is expected as LDAP requires explicit mapping of Windows SIDs to LDAP entries");
                    _logger.LogInformation("Expected cross-provider limitation: {Error}", ex.Message);
                }

                _logger.LogInformation("Completed Windows-LDAP cross-provider demo");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows-LDAP cross-provider demo failed: {ex.Message}");
                _logger.LogError(ex, "Error in Windows-LDAP cross-provider demo");
            }
        }
    }
}