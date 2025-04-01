using Microsoft.Extensions.Logging;
using System;

namespace ConsoleTester
{
    public class ProviderSelector : IProviderSelector
    {
        private readonly ILogger<ProviderSelector> _logger;

        public ProviderSelector(ILogger<ProviderSelector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ProviderSelections SelectProviders()
        {
            // Menu for selecting which providers to use
            Console.WriteLine("\nSelect directory providers to use:");
            Console.WriteLine("1. JSON File Provider (works without admin rights)");
            Console.WriteLine("2. Windows Local Provider (may require admin rights)");
            Console.WriteLine("3. Active Directory Provider (requires domain connectivity)");
            Console.WriteLine("4. LDAP.NET Provider (cross-platform)");
            Console.WriteLine("5. All Available Providers");
            Console.Write("Enter your choice (1-5): ");

            string choice = Console.ReadLine();
            ProviderSelections selections = new ProviderSelections
            {
                UseJson = choice == "1" || choice == "5",
                UseWindowsLocal = choice == "2" || choice == "5",
                UseActiveDirectory = choice == "3" || choice == "5",
                UseLdapNet = choice == "4" || choice == "5"
            };

            _logger.LogInformation(
                "Selected providers: {Providers}",
                string.Join(", ", selections.GetSelectedProviders()));

            return selections;
        }
    }
}