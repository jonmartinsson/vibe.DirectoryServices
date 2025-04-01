using ConsoleTester.Runners.Adsi;
using ConsoleTester.Runners.JsonFile;
using ConsoleTester.Runners.LdapNet;
using Microsoft.Extensions.Logging;
using System;
using vibe.DirectoryServices;

namespace ConsoleTester
{
    public class ProviderConfigurator
    {
        private readonly DirectoryServiceFactory _factory;
        private readonly JsonProviderConfigurator _jsonConfigurator;
        private readonly LdapProviderConfigurator _ldapConfigurator;
        private readonly ILogger<ProviderConfigurator> _logger;
        private readonly WindowsProviderConfigurator _windowsConfigurator;

        public ProviderConfigurator(
            DirectoryServiceFactory factory,
            ILogger<ProviderConfigurator> logger,
            JsonProviderConfigurator jsonConfigurator,
            WindowsProviderConfigurator windowsConfigurator,
            LdapProviderConfigurator ldapConfigurator)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonConfigurator = jsonConfigurator ?? throw new ArgumentNullException(nameof(jsonConfigurator));
            _windowsConfigurator = windowsConfigurator ?? throw new ArgumentNullException(nameof(windowsConfigurator));
            _ldapConfigurator = ldapConfigurator ?? throw new ArgumentNullException(nameof(ldapConfigurator));
        }

        public void ConfigureProviders(ProviderSelections selections)
        {
            if (selections.UseJson)
            {
                _jsonConfigurator.ConfigureAndRegister(_factory);
            }

            if (selections.UseWindowsLocal)
            {
                _windowsConfigurator.ConfigureLocalWindowsProvider(_factory);
            }

            if (selections.UseActiveDirectory)
            {
                _windowsConfigurator.ConfigureActiveDirectoryProvider(_factory);
            }

            if (selections.UseLdapNet)
            {
                _ldapConfigurator.ConfigureAndRegister(_factory);
            }
        }
    }
}