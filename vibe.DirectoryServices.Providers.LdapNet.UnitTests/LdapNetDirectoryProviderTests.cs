using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace vibe.DirectoryServices.Providers.LdapNet.IntegrationTests
{
    public class LdapNetDirectoryProviderTests : IDisposable
    {
        private readonly LdapNetDirectoryProviderConfiguration _config;
        private readonly ILoggerFactory _loggerFactory;
        private readonly LdapNetDirectoryProvider _provider;
        private readonly string _testUserPrefix = "test_user_";
        private readonly string _timestamp;

        public LdapNetDirectoryProviderTests()
        {
            LoadEnvironmentVariablesFromLaunchSettings("vibe.DirectoryServices.Providers.LdapNet.IntegrationTests");

            // Generate a unique timestamp to prevent collisions between test runs
            _timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

            // Setup logging
            _loggerFactory = new NullLoggerFactory();

            // Initialize LDAP configuration with environment variables or default test values
            _config = new LdapNetDirectoryProviderConfiguration
            {
                ServerAddress = Environment.GetEnvironmentVariable("LDAP_TEST_SERVER") ?? "localhost",
                ServerPort = int.Parse(Environment.GetEnvironmentVariable("LDAP_TEST_PORT") ?? "389"),
                BaseDn = Environment.GetEnvironmentVariable("LDAP_TEST_BASE_DN") ?? "dc=example,dc=com",
                //UsersDn = Environment.GetEnvironmentVariable("LDAP_TEST_USERS_DN") ?? "ou=users,dc=example,dc=com",
                //GroupsDN = Environment.GetEnvironmentVariable("LDAP_TEST_GROUPS_DN") ?? "ou=groups,dc=example,dc=com",
                Username = Environment.GetEnvironmentVariable("LDAP_TEST_BIND_DN") ?? "cn=admin,dc=example,dc=com",
                Password = Environment.GetEnvironmentVariable("LDAP_TEST_BIND_PASSWORD") ?? "admin",
                UseSsl = bool.Parse(Environment.GetEnvironmentVariable("LDAP_TEST_USE_SSL") ?? "false")
            };

            // Create the provider
            _provider = new LdapNetDirectoryProvider(_config, _loggerFactory.CreateLogger<LdapNetDirectoryProvider>());
        }

        public static string GetProjectDirectory()
        {
            string binDirectory = AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(binDirectory, "..", "..", ".."));
        }

        private void LoadEnvironmentVariablesFromLaunchSettings(string profileName)
        {
            try
            {
                string filePath = Path.Combine(GetProjectDirectory(), "Properties", "launchSettings.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    using JsonDocument doc = JsonDocument.Parse(json);

                    var environmentVariables = doc.RootElement
                        .GetProperty("profiles")
                        .GetProperty(profileName) // Use your profile name
                        .GetProperty("environmentVariables");

                    foreach (var property in environmentVariables.EnumerateObject())
                    {
                        Environment.SetEnvironmentVariable(property.Name, property.Value.GetString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading environment variables: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Cleanup - find and delete all test users created during this test run
            try
            {
//                CleanupTestUsers();
            }
            catch (Exception)
            {
                // Log but continue if cleanup fails
            }
        }

        [Fact]
        public void CreateUser_ValidParameters_CreatesUser()
        {
            // Arrange
            string username = $"{_testUserPrefix}{_timestamp}";
            string displayName = $"Test User {_timestamp}";
            string email = $"{username}@example.com";

            DirectoryUserCreationParams userParams = new DirectoryUserCreationParams
            {
                Username = username, DisplayName = displayName, Email = email
            };

            // Act
            IDirectoryUser<string> user = _provider.CreateUser(userParams);

            // Assert
            Assert.NotNull(user);
            Assert.Equal(username, user.Username);
            Assert.Equal(displayName, user.DisplayName);
            Assert.Equal("LdapNet", user.ProviderId);

            // Verify user exists by searching for it
            IDirectoryUser<string> foundUser = _provider.FindUser(username);
            Assert.NotNull(foundUser);
            Assert.Equal(username, foundUser.Username);
        }

#if false
        [Fact]
        public void DeleteUser_ExistingUser_DeletesUser()
        {
            // Arrange - Create a user to delete
            string username = $"{_testUserPrefix}{_timestamp}_delete";
            string displayName = $"Test User {_timestamp} Delete";
            string email = $"{username}@example.com";

            DirectoryUserCreationParams userParams = new DirectoryUserCreationParams
            {
                Username = username, DisplayName = displayName, Email = email
            };

            IDirectoryUser<string> user = _provider.CreateUser(userParams);
            Assert.NotNull(user);

            // Act
            _provider.DeleteUser(user);

            // Assert
            IDirectoryUser<string> foundUser = _provider.FindUser(username);
            Assert.Null(foundUser);
        }

        [Fact]
        public void DeleteUser_NonExistentUser_ThrowsKeyNotFoundException()
        {
            // Arrange
            string username = $"{_testUserPrefix}nonexistent_{_timestamp}";
            IDirectoryUser<string> nonExistentUser = new LdapNetUser(
                "cn=nonexistent," + _config.UsersDN,
                username,
                "Nonexistent User",
                "nonexistent@example.com");

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => _provider.DeleteUser(nonExistentUser));
        }

        [Fact]
        public void CreateUser_DuplicateUsername_ThrowsInvalidOperationException()
        {
            // Arrange
            string username = $"{_testUserPrefix}duplicate_{_timestamp}";
            string displayName = $"Duplicate Test User {_timestamp}";
            string email = $"{username}@example.com";

            DirectoryUserCreationParams userParams = new DirectoryUserCreationParams
            {
                Username = username, DisplayName = displayName, Email = email
            };

            // Create the user first time
            IDirectoryUser<string> user = _provider.CreateUser(userParams);
            Assert.NotNull(user);

            // Act & Assert - Try to create again with same username
            Assert.Throws<InvalidOperationException>(() => _provider.CreateUser(userParams));
        }

        private void CleanupTestUsers()
        {
            // Find all test users with our prefix and timestamp
            string testUserPrefix = $"{_testUserPrefix}";

            // Use implementation-specific search method to find all test users
            // This is a simplified approach - in a real implementation you'd use proper search functionality
            var testUsers = _provider.SearchUsers(testUserPrefix, UserSearchType.Username);

            foreach (var user in testUsers)
            {
                try
                {
                    _provider.DeleteUser(user);
                }
                catch (Exception)
                {
                    // Log but continue with other deletions
                }
            }
        }
#endif
    }
}