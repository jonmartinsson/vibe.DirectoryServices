using Microsoft.Extensions.Logging;
using System;
using System.DirectoryServices.Protocols;
using vibe.DirectoryServices;
using vibe.DirectoryServices.Providers.LdapNet;

namespace ConsoleTester.Runners.LdapNet
{
    public class LdapProviderConfigurator
    {
        private readonly ILogger<LdapProviderConfigurator> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public LdapProviderConfigurator(ILogger<LdapProviderConfigurator> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public void ConfigureAndRegister(DirectoryServiceFactory factory)
        {
            try
            {
                LdapNetDirectoryProviderConfiguration config = CollectLdapConfiguration();
                ILogger<LdapNetDirectoryProvider> logger = _loggerFactory.CreateLogger<LdapNetDirectoryProvider>();
                LdapNetDirectoryProvider provider = new LdapNetDirectoryProvider(config, logger);

                factory.RegisterProvider(provider);
                _logger.LogInformation(
                    $"Registered LDAP.NET provider (using '{config.ServerAddress}:{config.ServerPort}')");

                Console.WriteLine($"Registered LDAP.NET provider (using '{config.ServerAddress}:{config.ServerPort}')");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register LDAP.NET provider");
                Console.WriteLine($"Could not register LDAP.NET provider: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private LdapNetDirectoryProviderConfiguration CollectLdapConfiguration()
        {
            // Get LDAP server details
            Console.WriteLine("\nLDAP.NET Provider Configuration:");

            Console.Write("Enter LDAP server address [localhost]: ");
            string server = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(server))
            {
                server = "localhost";
            }

            Console.Write("Enter LDAP server port [389]: ");
            string portStr = Console.ReadLine();
            int port = string.IsNullOrWhiteSpace(portStr) ? 389 : int.Parse(portStr);

            Console.Write("Enter Base DN [dc=example,dc=com]: ");
            string baseDn = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(baseDn))
            {
                baseDn = "dc=example,dc=com";
            }

            Console.Write("Enter bind username [cn=admin,dc=example,dc=com]: ");
            string username = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "cn=admin,dc=example,dc=com";
            }

            Console.Write("Enter bind password: ");
            string password = Console.ReadLine();

            Console.Write("Use SSL (y/n) [n]: ");
            string useSslStr = Console.ReadLine();
            bool useSsl = useSslStr?.ToLowerInvariant() == "y";

            // Create and return the configuration
            return new LdapNetDirectoryProviderConfiguration
            {
                ServerAddress = server,
                ServerPort = port,
                BaseDn = baseDn,
                Username = username,
                Password = password,
                UseSsl = useSsl,
                AuthType = AuthType.Basic,
                AttributeMapping = new LdapAttributeMapping() // Use defaults
            };
        }
    }
}