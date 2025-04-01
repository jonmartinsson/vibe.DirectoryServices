using ConsoleTester.Runners;
using ConsoleTester.Runners.Adsi;
using ConsoleTester.Runners.JsonFile;
using ConsoleTester.Runners.LdapNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using vibe.DirectoryServices;

namespace ConsoleTester
{
    public class DemoRunner
    {
        private readonly DirectoryServiceFactory _factory;
        private readonly ILogger<DemoRunner> _logger;
        private readonly ProviderConfigurator _providerConfigurator;
        private readonly IProviderSelector _providerSelector;
        private readonly IServiceProvider _serviceProvider;

        public DemoRunner(
            IProviderSelector providerSelector,
            ProviderConfigurator providerConfigurator,
            DirectoryServiceFactory factory,
            ILogger<DemoRunner> logger,
            IServiceProvider serviceProvider)
        {
            _providerSelector = providerSelector ?? throw new ArgumentNullException(nameof(providerSelector));
            _providerConfigurator =
                providerConfigurator ?? throw new ArgumentNullException(nameof(providerConfigurator));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void Run()
        {
            // Let the user select providers
            ProviderSelections selections = _providerSelector.SelectProviders();

            // Configure the selected providers
            _providerConfigurator.ConfigureProviders(selections);

            try
            {
                // Run demos for selected providers
                RunProviderDemos(selections);

                // Run cross-provider demo if multiple providers are selected
                if (selections.IsMultipleProvidersSelected)
                {
                    RunCrossProviderDemo(selections);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running demos");
                Console.WriteLine($"\nError running demos: {ex.Message}");
            }
        }

        private void RunProviderDemos(ProviderSelections selections)
        {
            // Run JSON provider demo
            if (selections.UseJson)
            {
                JsonDemoRunner jsonDemoRunner = _serviceProvider.GetRequiredService<JsonDemoRunner>();
                jsonDemoRunner.Run(_factory);
            }

            // Run LDAP provider demo
            if (selections.UseLdapNet)
            {
                LdapDemoRunner ldapDemoRunner = _serviceProvider.GetRequiredService<LdapDemoRunner>();
                ldapDemoRunner.Run(_factory);
            }

            // Run Windows provider demo
            if (selections.UseWindowsLocal || selections.UseActiveDirectory)
            {
                WindowsDemoRunner windowsDemoRunner = _serviceProvider.GetRequiredService<WindowsDemoRunner>();
                windowsDemoRunner.Run(_factory);
            }
        }

        private void RunCrossProviderDemo(ProviderSelections selections)
        {
            CrossProviderDemoRunner crossProviderDemoRunner =
                _serviceProvider.GetRequiredService<CrossProviderDemoRunner>();
            crossProviderDemoRunner.Run(_factory, selections);
        }
    }
}