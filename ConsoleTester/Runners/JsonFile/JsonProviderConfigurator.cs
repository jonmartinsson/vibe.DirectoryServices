using Microsoft.Extensions.Logging;
using System;
using System.IO;
using vibe.DirectoryServices;
using vibe.DirectoryServices.Providers.JsonFile;

namespace ConsoleTester.Runners.JsonFile
{
    public class JsonProviderConfigurator
    {
        private readonly ILogger<JsonProviderConfigurator> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public JsonProviderConfigurator(ILogger<JsonProviderConfigurator> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public void ConfigureAndRegister(DirectoryServiceFactory factory)
        {
            string jsonFilePath = Path.Combine(Environment.CurrentDirectory, "directory-data.json");

            try
            {
                JsonDirectoryProviderConfiguration config =
                    new JsonDirectoryProviderConfiguration { FilePath = jsonFilePath };
                ILogger<JsonDirectoryProvider> logger = _loggerFactory.CreateLogger<JsonDirectoryProvider>();
                JsonDirectoryProvider provider = new JsonDirectoryProvider(config, logger);

                factory.RegisterProvider(provider);
                _logger.LogInformation($"Registered JSON File provider (using '{jsonFilePath}')");

                Console.WriteLine($"Registered JSON File provider (using '{jsonFilePath}')");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register JSON provider");
                Console.WriteLine($"Could not register JSON File provider: {ex.Message}");
            }
        }
    }
}