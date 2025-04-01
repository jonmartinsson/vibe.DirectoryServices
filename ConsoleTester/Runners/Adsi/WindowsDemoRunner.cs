using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using vibe.DirectoryServices;

namespace ConsoleTester.Runners.Adsi
{
    public class WindowsDemoRunner
    {
        private readonly IDirectoryService<string> _directoryService;
        private readonly ILogger<WindowsDemoRunner> _logger;

        public WindowsDemoRunner(
            ILogger<WindowsDemoRunner> logger,
            IDirectoryService<string> directoryService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        }

        public void Run(DirectoryServiceFactory factory)
        {
            try
            {
                Console.WriteLine("\nSearching for existing users in Windows providers:");
                _logger.LogInformation("Starting Windows provider demo");

                // Search for users with "admin" in their name
                IEnumerable<IDirectoryUser<string>> users =
                    _directoryService.SearchUsers("admin", UserSearchType.Username);

                foreach (IDirectoryUser<string> user in users)
                {
                    if (user.ProviderId == "WindowsLocal" || user.ProviderId == "ActiveDirectory")
                    {
                        Console.WriteLine($"Found user: {user.DisplayName} ({user.ProviderId})");
                        _logger.LogDebug("Found Windows user: {Username}, {Provider}, {Sid}",
                            user.DisplayName, user.ProviderId, user.Sid);
                    }
                }

                _logger.LogInformation("Completed Windows provider demo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Windows provider demo");
                Console.WriteLine($"Error in Windows provider demo: {ex.Message}");
            }
        }
    }
}