using Microsoft.Extensions.Logging;
using System;
using vibe.DirectoryServices;

namespace ConsoleTester.Runners.JsonFile
{
    public class JsonDemoRunner
    {
        private readonly ILogger<JsonDemoRunner> _logger;

        public JsonDemoRunner(ILogger<JsonDemoRunner> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(DirectoryServiceFactory factory)
        {
            try
            {
                IDirectoryProvider<string> jsonProvider = factory.GetProvider("JsonFile");

                Console.WriteLine("\nCreating users and groups with JSON provider...");
                _logger.LogInformation("Starting JSON provider demo");

                // Create users
                IDirectoryUser<string> user1 = jsonProvider.CreateUser(new DirectoryUserCreationParams
                {
                    Username = "jsmith", DisplayName = "John Smith", Email = "john.smith@example.com"
                });
                _logger.LogDebug("Created user: {Username}, {DisplayName}", user1.Username, user1.DisplayName);

                IDirectoryUser<string> user2 = jsonProvider.CreateUser(new DirectoryUserCreationParams
                {
                    Username = "mjones", DisplayName = "Mary Jones", Email = "mary.jones@example.com"
                });
                _logger.LogDebug("Created user: {Username}, {DisplayName}", user2.Username, user2.DisplayName);

                // Create groups
                IDirectoryGroup<string> group1 = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
                {
                    GroupName = "Administrators", Description = "System Administrators"
                });
                _logger.LogDebug("Created group: {GroupName}", group1.GroupName);

                IDirectoryGroup<string> group2 = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
                {
                    GroupName = "Users", Description = "Regular Users"
                });
                _logger.LogDebug("Created group: {GroupName}", group2.GroupName);

                // Set up memberships
                group1.AddMember(user1);
                _logger.LogDebug("Added {User} to {Group}", user1.DisplayName, group1.GroupName);

                group2.AddMember(user2);
                _logger.LogDebug("Added {User} to {Group}", user2.DisplayName, group2.GroupName);

                // Test nested group
                IDirectoryGroup<string> parentGroup = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
                {
                    GroupName = "All Groups", Description = "Parent of all groups"
                });
                _logger.LogDebug("Created parent group: {GroupName}", parentGroup.GroupName);

                parentGroup.AddMember(group1);
                _logger.LogDebug("Added {Group} to {ParentGroup}", group1.GroupName, parentGroup.GroupName);

                parentGroup.AddMember(group2);
                _logger.LogDebug("Added {Group} to {ParentGroup}", group2.GroupName, parentGroup.GroupName);

                // Verify memberships
                Console.WriteLine("\nGroup memberships:");
                Console.WriteLine($"Is {user1.DisplayName} in {group1.GroupName}? {group1.IsMember(user1)}");
                Console.WriteLine($"Is {user2.DisplayName} in {group2.GroupName}? {group2.IsMember(user2)}");
                Console.WriteLine($"Is {group1.GroupName} in {parentGroup.GroupName}? {parentGroup.IsMember(group1)}");
                Console.WriteLine($"Is {user1.DisplayName} in {parentGroup.GroupName}? {parentGroup.IsMember(user1)}");

                _logger.LogInformation("Completed JSON provider demo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JSON provider demo");
                Console.WriteLine($"Error in JSON provider demo: {ex.Message}");
            }
        }
    }
}