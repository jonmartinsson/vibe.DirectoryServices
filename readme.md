# Directory Services Abstraction

This project implements a C# abstraction for handling users and groups in different directory services. It provides a unified interface for working with users and groups across multiple directory systems, including Active Directory, local Windows users/groups, and custom JSON storage.

## Overview

The abstraction is built around the following core interfaces:

- `IDirectoryService<TSid>`: High-level service for searching users and resolving group memberships
- `IDirectoryProvider<TSid>`: Provider implementation for specific directory systems
- `IDirectoryUser<TSid>`: Represents a user entity in any directory system
- `IDirectoryGroup<TSid>`: Represents a group entity that can contain users and other groups

Where `TSid` is a generic type parameter representing the security identifier format used by the provider (typically `string`).

## Supported Providers

The current implementation includes three directory providers:

1. `ActiveDirectoryProvider`: For handling Active Directory users and groups
2. `LocalWindowsProvider`: For managing local Windows users and groups
3. `JsonProvider`: For storing and managing users and groups in a local JSON file

Both the `ActiveDirectoryProvider` and `LocalWindowsProvider` inherit from the common base class `AdsiDirectoryProvider`, which leverages the `System.DirectoryServices.AccountManagement` namespace to work with Windows identity systems.

## Cross-Provider Support

A key feature of this abstraction is the ability to add members from one provider to groups in another provider when possible:

- Active Directory groups can include local Windows users through foreign security principals
- Local Windows groups can include Active Directory users
- JSON provider groups can include members from any other provider

Each provider implements a `CanHandleForeignEntity()` method that determines whether it can handle entities from other providers.

## Factory Pattern

The system uses a `DirectoryServiceFactory` to create and manage directory service instances:

```csharp
// Create a factory with logging
DirectoryServiceFactory factory = new DirectoryServiceFactory(loggerFactory);

// Register providers
factory.RegisterProvider(new JsonProvider(config, loggerFactory));
factory.RegisterProvider(new LocalWindowsProvider());
factory.RegisterProvider(new ActiveDirectoryProvider());

// Get the directory service
IDirectoryService<string> directoryService = factory.GetService();
```

## Provider Implementation Details

### ActiveDirectoryProvider

Connects to Active Directory using domain context and provides access to AD users and groups. Uses `ActiveDirectoryUser` and `ActiveDirectoryGroup` concrete classes.

### LocalWindowsProvider

Manages local Windows users and groups using machine context. Uses `LocalWindowsUser` and `LocalWindowsGroup` concrete classes.

### JsonProvider

A file-based provider that stores users and groups in a JSON file. This provider can reference users and groups from any other provider. Uses `JsonUser` and `JsonGroup` concrete classes.

## Logging

The system includes comprehensive logging using the Microsoft.Extensions.Logging framework. Each provider and the directory service log important operations, errors, and debug information.

## Usage Example

```csharp
// Create a factory with logging
ILoggerFactory loggerFactory = CreateLoggerFactory(); // Your logging setup
DirectoryServiceFactory factory = new DirectoryServiceFactory(loggerFactory);

// Register providers based on your needs
if (useJson)
{
    string jsonFilePath = Path.Combine(Environment.CurrentDirectory, "directory-data.json");
    factory.RegisterProvider(new JsonProvider(
        new JsonProviderConfiguration { FilePath = jsonFilePath }, 
        loggerFactory
    ));
}

if (useWindowsLocal)
{
    factory.RegisterProvider(new LocalWindowsProvider());
}

if (useActiveDirectory)
{
    factory.RegisterProvider(new ActiveDirectoryProvider());
}

// Get the directory service
IDirectoryService<string> directoryService = factory.GetService();

// Search for users
foreach (IDirectoryUser<string> user in directoryService.SearchUsers("admin", UserSearchType.Username))
{
    Console.WriteLine($"Found user: {user.DisplayName} ({user.ProviderId})");
}

// Create users and groups with a specific provider
IDirectoryProvider<string> jsonProvider = factory.GetProvider("JsonFile");
IDirectoryUser<string> user = jsonProvider.CreateUser(new DirectoryUserCreationParams
{
    Username = "jsmith", 
    DisplayName = "John Smith", 
    Email = "john.smith@example.com"
});

IDirectoryGroup<string> group = jsonProvider.CreateGroup(new DirectoryGroupCreationParams
{
    GroupName = "Administrators", 
    Description = "System Administrators"
});

// Add user to group
group.AddMember(user);
```

## Requirements

- .NET 8.0
- Microsoft.Extensions.Logging.Abstractions (8.0.1)
- System.DirectoryServices.AccountManagement (8.0.1) for Windows directory access

## Future Enhancements

Potential future enhancements could include:
- A generic LDAP provider for connecting to other directory services
- A common base class for all providers
- Support for additional entity properties and operations
- Asynchronous operation support

## License

[Your license information here]