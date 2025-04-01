using System;
using System.IO;
using vibe.DirectoryServices;
using vibe.DirectoryServices.Providers.Adsi;
using vibe.DirectoryServices.Providers.JsonFile;
using Microsoft.Extensions.Logging;

namespace ConsoleTester
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Directory Services Example");
            Console.WriteLine("=========================");

            // Create a factory for directory services with logging
            ILoggerFactory loggerFactory = null;
            DirectoryServiceFactory factory = new DirectoryServiceFactory(loggerFactory);

            // Menu for selecting which providers to use
            Console.WriteLine("\nSelect directory providers to use:");
            Console.WriteLine("1. JSON File Provider (works without admin rights)");
            Console.WriteLine("2. Windows Local Provider (may require admin rights)");
            Console.WriteLine("3. Active Directory Provider (requires domain connectivity)");
            Console.WriteLine("4. All Available Providers");
            Console.Write("Enter your choice (1-4): ");

            string choice = Console.ReadLine();
            bool useJson = choice == "1" || choice == "4";
            bool useWindowsLocal = choice == "2" || choice == "4";
            bool useActiveDirectory = choice == "3" || choice == "4";

            // Register selected providers
            if (useJson)
            {
                string jsonFilePath = Path.Combine(Environment.CurrentDirectory, "directory-data.json");
                factory.RegisterProvider(new JsonProvider(new JsonProviderConfiguration() { FilePath = jsonFilePath }, loggerFactory));
                Console.WriteLine($"Registered JSON File provider (using '{jsonFilePath}')");
            }

            if (useWindowsLocal)
            {
                try
                {
                    factory.RegisterProvider(new LocalWindowsProvider());
                    Console.WriteLine("Registered Windows Local provider");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not register Windows Local provider: {ex.Message}");
                    Console.WriteLine("(This may require elevated permissions)");
                }
            }

            if (useActiveDirectory)
            {
                try
                {
                    factory.RegisterProvider(new ActiveDirectoryProvider());
                    Console.WriteLine("Registered Active Directory provider");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not register Active Directory provider: {ex.Message}");
                    Console.WriteLine("(This may require domain connectivity and permissions)");
                }
            }

            // Get the directory service
            IDirectoryService<string> directoryService = factory.GetService();

            try
            {
                // If JSON provider is registered, run a demo with it
                if (useJson)
                {
                    IDirectoryProvider<string> jsonProvider = factory.GetProvider("JsonFile");
                    Console.WriteLine("\nCreating users and groups with JSON provider...");

                    // Create users
                    IDirectoryUser<string> user1 = jsonProvider.CreateUser(new DirectoryUserCreationParams
                    {
                        Username = "jsmith", DisplayName = "John Smith", Email = "john.smith@example.com"
                    });

                    IDirectoryUser<string> user2 = jsonProvider.CreateUser(new DirectoryUserCreationParams
                    {
                        Username = "mjones", DisplayName = "Mary Jones", Email = "mary.jones@example.com"
                    });

                    // Create groups
                    IDirectoryGroup<string> group1 = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
                    {
                        GroupName = "Administrators", Description = "System Administrators"
                    });

                    IDirectoryGroup<string> group2 = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
                    {
                        GroupName = "Users", Description = "Regular Users"
                    });

                    // Set up memberships
                    group1.AddMember(user1);
                    group2.AddMember(user2);

                    // Test nested group
                    IDirectoryGroup<string> parentGroup = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
                    {
                        GroupName = "All Groups", Description = "Parent of all groups"
                    });

                    parentGroup.AddMember(group1);
                    parentGroup.AddMember(group2);

                    // Verify memberships
                    Console.WriteLine("\nGroup memberships:");
                    Console.WriteLine($"Is {user1.DisplayName} in {group1.GroupName}? {group1.IsMember(user1)}");
                    Console.WriteLine($"Is {user2.DisplayName} in {group2.GroupName}? {group2.IsMember(user2)}");
                    Console.WriteLine(
                        $"Is {group1.GroupName} in {parentGroup.GroupName}? {parentGroup.IsMember(group1)}");
                    Console.WriteLine(
                        $"Is {user1.DisplayName} in {parentGroup.GroupName}? {parentGroup.IsMember(user1)}");
                }

                // If Windows Local or Active Directory providers are registered, try to find some users
                if (useWindowsLocal || useActiveDirectory)
                {
                    Console.WriteLine("\nSearching for existing users in registered providers:");

                    foreach (IDirectoryUser<string> user in directoryService.SearchUsers("admin",
                                 UserSearchType.Username))
                    {
                        Console.WriteLine($"Found user: {user.DisplayName} ({user.ProviderId})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}