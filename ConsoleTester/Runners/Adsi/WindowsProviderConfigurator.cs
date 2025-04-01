using Microsoft.Extensions.Logging;
using System;
using vibe.DirectoryServices;
using vibe.DirectoryServices.Providers.Adsi;

namespace ConsoleTester.Runners.Adsi
{
    public class WindowsProviderConfigurator
    {
        private readonly ILogger<WindowsProviderConfigurator> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public WindowsProviderConfigurator(ILogger<WindowsProviderConfigurator> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public void ConfigureLocalWindowsProvider(DirectoryServiceFactory factory)
        {
            try
            {
                LocalWindowsDirectoryProviderConfiguration config = new LocalWindowsDirectoryProviderConfiguration();
                ILogger<LocalWindowsDirectoryProvider> logger =
                    _loggerFactory.CreateLogger<LocalWindowsDirectoryProvider>();
                LocalWindowsDirectoryProvider provider = new LocalWindowsDirectoryProvider(config, logger);

                factory.RegisterProvider(provider);
                _logger.LogInformation("Registered Windows Local provider");

                Console.WriteLine("Registered Windows Local provider");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register Windows Local provider");
                Console.WriteLine($"Could not register Windows Local provider: {ex.Message}");
                Console.WriteLine("(This may require elevated permissions)");
            }
        }

        public void ConfigureActiveDirectoryProvider(DirectoryServiceFactory factory)
        {
            try
            {
                ActiveDirectoryDirectoryProviderConfiguration config =
                    new ActiveDirectoryDirectoryProviderConfiguration();
                ILogger<ActiveDirectoryDirectoryProvider> logger =
                    _loggerFactory.CreateLogger<ActiveDirectoryDirectoryProvider>();
                ActiveDirectoryDirectoryProvider provider = new ActiveDirectoryDirectoryProvider(config, logger);

                factory.RegisterProvider(provider);
                _logger.LogInformation("Registered Active Directory provider");

                Console.WriteLine("Registered Active Directory provider");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register Active Directory provider");
                Console.WriteLine($"Could not register Active Directory provider: {ex.Message}");
                Console.WriteLine("(This may require domain connectivity and permissions)");
            }
        }
    }
}