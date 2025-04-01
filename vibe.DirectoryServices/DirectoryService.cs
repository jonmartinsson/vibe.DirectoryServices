using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace vibe.DirectoryServices
{
    public class DirectoryService<TSid> : IDirectoryService<TSid>
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IDirectoryProvider<TSid>> _providers;

        public DirectoryService(IEnumerable<IDirectoryProvider<TSid>> providers, ILogger logger)
        {
            if (providers == null || !providers.Any())
            {
                throw new ArgumentException("At least one provider must be specified", nameof(providers));
            }

            _providers = providers.ToDictionary(p => p.ProviderId, p => p);
            _logger = logger ?? throw new ArgumentException("Logger must be specified", nameof(logger));

            _logger.LogInformation(
                $"Directory service initialized with {_providers.Count} providers: {string.Join(", ", _providers.Keys)}");
        }

        public virtual IEnumerable<IDirectoryUser<TSid>> SearchUsers(string searchTerm, UserSearchType searchType)
        {
            _logger.LogDebug($"Searching for users with term '{searchTerm}' using search type '{searchType}'");

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogWarning("SearchUsers called with null or empty search term");
                throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));
            }

            List<IDirectoryUser<TSid>> results = new List<IDirectoryUser<TSid>>();
            List<Exception> errors = new List<Exception>();

            foreach (IDirectoryProvider<TSid> provider in _providers.Values)
            {
                try
                {
                    _logger.LogDebug($"Searching provider '{provider.ProviderId}' for users matching '{searchTerm}'");
                    IDirectoryUser<TSid> user = provider.FindUser(searchTerm);
                    if (user != null)
                    {
                        _logger.LogDebug($"Found user '{user.Username}' in provider '{provider.ProviderId}'");
                        results.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error searching users in provider '{provider.ProviderId}'", ex);
                    errors.Add(new DirectoryServiceException(provider.ProviderId,
                        $"Error searching for users with term '{searchTerm}'", ex));
                }
            }

            if (results.Count == 0 && errors.Count > 0)
            {
                if (errors.Count == 1)
                {
                    throw errors[0];
                }

                throw new AggregateException("Multiple errors occurred while searching for users", errors);
            }

            _logger.LogInformation($"Found {results.Count} users matching term '{searchTerm}'");
            return results;
        }

        public virtual IDirectoryUser<TSid> GetUserById(TSid sid)
        {
            _logger.LogDebug($"Looking up user with SID '{sid}'");

            if (sid == null)
            {
                _logger.LogWarning("GetUserById called with null SID");
                throw new ArgumentNullException(nameof(sid), "SID cannot be null");
            }

            List<IDirectoryProvider<TSid>> supportingProviders =
                _providers.Values.Where(p => p.SupportsSidLookup(sid)).ToList();

            if (supportingProviders.Count == 0)
            {
                _logger.LogWarning($"No providers found that support SID format '{sid}'");
                throw new NotSupportedException($"No providers registered that support SID format: {sid}");
            }

            _logger.LogDebug($"Found {supportingProviders.Count} providers that support SID format '{sid}'");

            List<Exception> errors = new List<Exception>();

            foreach (IDirectoryProvider<TSid> provider in supportingProviders)
            {
                try
                {
                    _logger.LogDebug($"Checking provider '{provider.ProviderId}' for user with SID '{sid}'");
                    IDirectoryUser<TSid> user = provider.GetUserBySid(sid);
                    if (user != null)
                    {
                        _logger.LogInformation(
                            $"Found user '{user.Username}' with SID '{sid}' in provider '{provider.ProviderId}'");
                        return user;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error getting user with SID '{sid}' from provider '{provider.ProviderId}'",
                        ex);
                    errors.Add(new DirectoryServiceException(provider.ProviderId,
                        $"Error looking up user with SID '{sid}'", ex));
                }
            }

            if (errors.Count > 0)
            {
                if (errors.Count == 1)
                {
                    throw errors[0];
                }

                throw new AggregateException($"Multiple errors occurred while looking up user with SID '{sid}'",
                    errors);
            }

            _logger.LogWarning($"No user found with SID '{sid}' in any provider");
            throw new KeyNotFoundException($"No user found with SID: {sid}");
        }

        public virtual IDirectoryGroup<TSid> GetGroupById(TSid sid)
        {
            _logger.LogDebug($"Looking up group with SID '{sid}'");

            if (sid == null)
            {
                _logger.LogWarning("GetGroupById called with null SID");
                throw new ArgumentNullException(nameof(sid), "SID cannot be null");
            }

            List<IDirectoryProvider<TSid>> supportingProviders =
                _providers.Values.Where(p => p.SupportsSidLookup(sid)).ToList();

            if (supportingProviders.Count == 0)
            {
                _logger.LogWarning($"No providers found that support SID format '{sid}'");
                throw new NotSupportedException($"No providers registered that support SID format: {sid}");
            }

            _logger.LogDebug($"Found {supportingProviders.Count} providers that support SID format '{sid}'");

            List<Exception> errors = new List<Exception>();

            foreach (IDirectoryProvider<TSid> provider in supportingProviders)
            {
                try
                {
                    _logger.LogDebug($"Checking provider '{provider.ProviderId}' for group with SID '{sid}'");
                    IDirectoryGroup<TSid> group = provider.GetGroupBySid(sid);
                    if (group != null)
                    {
                        _logger.LogInformation(
                            $"Found group '{group.GroupName}' with SID '{sid}' in provider '{provider.ProviderId}'");
                        return group;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error getting group with SID '{sid}' from provider '{provider.ProviderId}'", ex);
                    errors.Add(new DirectoryServiceException(provider.ProviderId,
                        $"Error looking up group with SID '{sid}'", ex));
                }
            }

            if (errors.Count > 0)
            {
                if (errors.Count == 1)
                {
                    throw errors[0];
                }

                throw new AggregateException($"Multiple errors occurred while looking up group with SID '{sid}'",
                    errors);
            }

            _logger.LogWarning($"No group found with SID '{sid}' in any provider");
            throw new KeyNotFoundException($"No group found with SID: {sid}");
        }
    }
}