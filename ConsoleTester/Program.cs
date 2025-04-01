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
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Directory Services Example");
            Console.WriteLine("=========================");

            // Set up dependency injection container
            ServiceProvider serviceProvider = ConfigureServices();

            try
            {
                // Get the demo runner
                DemoRunner demoRunner = serviceProvider.GetRequiredService<DemoRunner>();

                // Run the selected demo
                demoRunner.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static ServiceProvider ConfigureServices()
        {
            // Create service collection
            ServiceCollection services = new ServiceCollection();

            // Configure logging
            services.AddLogging(configure => configure
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug));

            // Register directory services
            services.AddSingleton<DirectoryServiceFactory>();
            services.AddSingleton<IProviderSelector, ProviderSelector>();
            services.AddSingleton(sp =>
            {
                DirectoryServiceFactory factory = sp.GetRequiredService<DirectoryServiceFactory>();
                return factory.GetService();
            });

            // Register provider configurations
            services.AddSingleton<ProviderConfigurator>();
            services.AddSingleton<JsonProviderConfigurator>();
            services.AddSingleton<WindowsProviderConfigurator>();
            services.AddSingleton<LdapProviderConfigurator>();

            // Register demo runners
            services.AddSingleton<DemoRunner>();
            services.AddSingleton<JsonDemoRunner>();
            services.AddSingleton<LdapDemoRunner>();
            services.AddSingleton<WindowsDemoRunner>();
            services.AddSingleton<CrossProviderDemoRunner>();

            // Build and return the service provider
            return services.BuildServiceProvider();
        }
    }
}