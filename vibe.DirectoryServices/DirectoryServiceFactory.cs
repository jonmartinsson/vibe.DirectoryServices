using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace vibe.DirectoryServices
{
    public class DirectoryServiceFactory
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IDirectoryProvider<string>> _providerRegistry;
        private IDirectoryService<string> _serviceInstance;

        public DirectoryServiceFactory(ILoggerFactory loggerFactory)
        {
            _providerRegistry = new Dictionary<string, IDirectoryProvider<string>>();
            _logger = loggerFactory.CreateLogger<DirectoryServiceFactory>();
            _logger.LogInformation("Directory service factory initialized");
        }

        public void RegisterProvider(IDirectoryProvider<string> provider)
        {
            if (provider == null)
            {
                _logger.LogWarning("Attempted to register null provider");
                throw new ArgumentNullException(nameof(provider));
            }

            _logger.LogInformation($"Registering provider: {provider.ProviderId}");
            _providerRegistry[provider.ProviderId] = provider;

            // Invalidate any existing service instance since providers have changed
            if (_serviceInstance != null)
            {
                _logger.LogDebug("Invalidating existing service instance due to provider registration");
                _serviceInstance = null;
            }
        }

        public IDirectoryService<string> GetService()
        {
            if (_serviceInstance == null)
            {
                _logger.LogInformation($"Creating new directory service with {_providerRegistry.Count} providers");

                if (_providerRegistry.Count == 0)
                {
                    _logger.LogWarning("Attempting to create a service with no registered providers");
                    throw new InvalidOperationException("No providers have been registered");
                }

                _serviceInstance = new DirectoryService<string>(_providerRegistry.Values, _logger);
            }

            return _serviceInstance;
        }

        public IDirectoryProvider<string> GetProvider(string providerId)
        {
            _logger.LogDebug($"Looking up provider: {providerId}");

            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.LogWarning("Attempted to get provider with null or empty ID");
                throw new ArgumentException("Provider ID cannot be null or empty", nameof(providerId));
            }

            if (!_providerRegistry.TryGetValue(providerId, out var provider))
            {
                _logger.LogWarning($"Provider not found: {providerId}");
                throw new KeyNotFoundException($"No provider registered with ID: {providerId}");
            }

            return provider;
        }

        public bool HasProvider(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return false;

            return _providerRegistry.ContainsKey(providerId);
        }
    }
}