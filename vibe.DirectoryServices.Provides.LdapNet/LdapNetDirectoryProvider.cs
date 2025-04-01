using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;

namespace vibe.DirectoryServices.Providers.LdapNet
{
    /// <summary>
    ///     LDAP.NET directory provider for cross-platform compatibility
    /// </summary>
    public class LdapNetDirectoryProvider : DirectoryProviderBase<string>
    {
        private readonly LdapNetDirectoryProviderConfiguration _config;
        private readonly Dictionary<string, LdapEntry> _groupCache = new Dictionary<string, LdapEntry>();

        public LdapNetDirectoryProvider(LdapNetDirectoryProviderConfiguration config, ILogger<LdapNetDirectoryProvider> logger)
            : base(logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrEmpty(_config.ServerAddress))
            {
                throw new ArgumentException("Server address must be specified", nameof(config.ServerAddress));
            }

            if (string.IsNullOrEmpty(_config.BaseDn))
            {
                throw new ArgumentException("Base DN must be specified", nameof(config.BaseDn));
            }

            _logger.LogInformation(
                $"Initializing LDAP.NET provider with server: {_config.ServerAddress}:{_config.ServerPort}");
        }

        public override string ProviderId => "LdapNet";

        private LdapConnection GetConnection()
        {
            LdapDirectoryIdentifier directoryIdentifier = new LdapDirectoryIdentifier(
                _config.ServerAddress,
                _config.ServerPort,
                true,
                false);

            NetworkCredential credentials = new NetworkCredential(
                _config.Username,
                _config.Password);

            LdapConnection connection = new LdapConnection(directoryIdentifier, credentials, _config.AuthType);

            if (_config.UseSsl)
            {
                connection.SessionOptions.SecureSocketLayer = true;
                connection.SessionOptions.VerifyServerCertificate =
                    (conn, cert) => true; // In production, use proper certificate validation
            }

            connection.Bind();
            return connection;
        }

        public override bool SupportsSidLookup(string sid)
        {
            // LDAP.NET provider can handle SIDs that start with "L-"
            return sid != null && sid.StartsWith("L-");
        }

        public override bool CanHandleForeignEntity(IDirectoryEntity<string> entity)
        {
            // The LDAP provider can handle its own entities by default
            if (entity.ProviderId == ProviderId)
            {
                return true;
            }

            // It may be able to handle foreign entities if they can be mapped to LDAP DNs
            // This is highly dependent on your directory setup and cross-directory references

            // Example: If you have mappings between Active Directory SIDs and LDAP entries
            if (entity.ProviderId == "ActiveDirectory" || entity.ProviderId == "WindowsLocal")
            {
                // Check if we can map this entity
                string dn = MapForeignEntityToDn(entity);
                return !string.IsNullOrEmpty(dn);
            }

            // For JSON provider entities, they might be mapped to LDAP entries as well
            if (entity.ProviderId == "JsonFile")
            {
                // Similar logic to check if we can map the JSON entity to an LDAP entry
                return false; // By default, return false unless you implement specific mapping
            }

            return false;
        }

        protected class LdapEntry
        {
            public string Dn { get; set; }
            public Dictionary<string, List<string>> Attributes { get; set; }
        }


        #region GroupOps

        public override IDirectoryGroup<string> CreateGroup(DirectoryGroupCreationParams parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.GroupName))
            {
                throw new ArgumentException("Group name cannot be empty", nameof(parameters));
            }

            _logger.LogInformation($"Creating group with name: {parameters.GroupName}");

            // Generate a unique DN for the new group using configured RDN attribute
            string rdnAttribute = _config.AttributeMapping.GroupRdnAttribute;
            string groupDn = $"{rdnAttribute}={parameters.GroupName},{_config.BaseDn}";

            // Check if group already exists
            if (FindGroupByDn(groupDn) != null)
            {
                _logger.LogWarning($"Group with DN '{groupDn}' already exists");
                throw new InvalidOperationException($"Group with DN '{groupDn}' already exists");
            }

            // Generate a unique identifier (SID equivalent in LDAP)
            string sid = $"L-{Guid.NewGuid().ToString("N")}";

            // Prepare group attributes
            List<DirectoryAttribute> attributes = new List<DirectoryAttribute>
            {
                new DirectoryAttribute("objectClass", "top", _config.AttributeMapping.GroupObjectClass),
                new DirectoryAttribute(rdnAttribute, parameters.GroupName),
                new DirectoryAttribute(_config.AttributeMapping.GroupNameAttribute, parameters.GroupName),
                new DirectoryAttribute(_config.AttributeMapping.GroupDescriptionAttribute,
                    parameters.Description ?? ""),
                new DirectoryAttribute(_config.AttributeMapping.SidAttribute, sid), // Store our SID equivalent
                new DirectoryAttribute(_config.AttributeMapping.GroupMemberAttribute,
                    "") // Required attribute for groups, initially empty
            };

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Create the new group entry
                    AddRequest addRequest = new AddRequest(groupDn, attributes.ToArray());
                    AddResponse addResponse = (AddResponse)connection.SendRequest(addRequest);

                    if (addResponse.ResultCode != ResultCode.Success)
                    {
                        throw new LdapNetProviderException($"Failed to create group: {addResponse.ErrorMessage}");
                    }

                    _logger.LogDebug($"Group created with DN: {groupDn} and SID: {sid}");

                    LdapNetGroup group = new LdapNetGroup(
                        sid,
                        parameters.GroupName,
                        parameters.Description ?? "",
                        groupDn,
                        this
                    );

                    // Cache the group
                    SearchResultEntry entry = GetLdapEntry(groupDn);
                    CacheGroup(group, ConvertToLdapEntry(entry));

                    return group;
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error creating group: {ex.Message}");
                throw new LdapNetProviderException($"Failed to create group: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating group: {ex.Message}");
                throw new LdapNetProviderException($"Error creating group: {ex.Message}", ex);
            }
        }

        public override IDirectoryGroup<string> FindGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("Group name cannot be empty", nameof(groupName));
            }

            _logger.LogDebug($"Looking up group with name: {groupName}");

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Search for group with the given name using configured attributes
                    string groupObjectClass = _config.AttributeMapping.GroupObjectClass;
                    string groupNameAttr = _config.AttributeMapping.GroupNameAttribute;
                    string rdnAttr = _config.AttributeMapping.GroupRdnAttribute;
                    string sidAttr = _config.AttributeMapping.SidAttribute;

                    // Build filter to search by group name
                    string filter =
                        $"(&(objectClass={groupObjectClass})(|({groupNameAttr}={groupName})({rdnAttr}={groupName}))({sidAttr}=*))";

                    // Prepare list of attributes to retrieve
                    string[] returnAttributes =
                    {
                        sidAttr, rdnAttr, groupNameAttr, _config.AttributeMapping.GroupDescriptionAttribute,
                        _config.AttributeMapping.GroupMemberAttribute
                    };

                    SearchRequest searchRequest = new SearchRequest(
                        _config.BaseDn,
                        filter,
                        SearchScope.Subtree,
                        returnAttributes
                    );

                    SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count == 0)
                    {
                        _logger.LogDebug($"Group not found with name: {groupName}");
                        return null;
                    }

                    // Use the first matching entry
                    SearchResultEntry entry = searchResponse.Entries[0];

                    string sid = GetAttributeValue(entry, sidAttr);
                    string name = GetAttributeValue(entry, groupNameAttr) ?? GetAttributeValue(entry, rdnAttr);
                    string description = GetAttributeValue(entry, _config.AttributeMapping.GroupDescriptionAttribute) ??
                                         "";

                    _logger.LogDebug($"Found group with name: {groupName}, SID: {sid}");

                    LdapNetGroup group = new LdapNetGroup(
                        sid,
                        name,
                        description,
                        entry.DistinguishedName,
                        this
                    );

                    // Cache the group entry
                    CacheGroup(group, ConvertToLdapEntry(entry));

                    return group;
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error finding group: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding group: {ex.Message}");
                return null;
            }
        }

        public override IDirectoryGroup<string> GetGroupBySid(string sid)
        {
            if (string.IsNullOrEmpty(sid))
            {
                throw new ArgumentNullException(nameof(sid));
            }

            if (!SupportsSidLookup(sid))
            {
                return null;
            }

            _logger.LogDebug($"Looking up group with SID: {sid}");

            // Check cache first
            if (_groupCache.TryGetValue(sid, out LdapEntry cachedEntry))
            {
                _logger.LogDebug($"Found cached group with SID: {sid}");

                string groupName = cachedEntry.Attributes.ContainsKey(_config.AttributeMapping.GroupNameAttribute)
                    ? cachedEntry.Attributes[_config.AttributeMapping.GroupNameAttribute].FirstOrDefault()
                    : null;

                string description =
                    cachedEntry.Attributes.ContainsKey(_config.AttributeMapping.GroupDescriptionAttribute)
                        ? cachedEntry.Attributes[_config.AttributeMapping.GroupDescriptionAttribute].FirstOrDefault()
                        : "";

                return new LdapNetGroup(
                    sid,
                    groupName ?? cachedEntry.Dn.Split(',')[0].Split('=')[1],
                    description ?? "",
                    cachedEntry.Dn,
                    this
                );
            }

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Search for group with the given SID using configured attributes
                    string groupObjectClass = _config.AttributeMapping.GroupObjectClass;
                    string sidAttr = _config.AttributeMapping.SidAttribute;

                    // Build filter to search by SID
                    string filter = $"(&(objectClass={groupObjectClass})({sidAttr}={sid}))";

                    SearchRequest searchRequest = new SearchRequest(
                        _config.BaseDn,
                        filter,
                        SearchScope.Subtree,
                        null // Retrieve all attributes
                    );

                    SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count == 0)
                    {
                        _logger.LogDebug($"Group not found with SID: {sid}");
                        return null;
                    }

                    // Use the first matching entry
                    SearchResultEntry entry = searchResponse.Entries[0];

                    string groupName = GetAttributeValue(entry, _config.AttributeMapping.GroupNameAttribute) ??
                                       GetAttributeValue(entry, _config.AttributeMapping.GroupRdnAttribute);
                    string description = GetAttributeValue(entry, _config.AttributeMapping.GroupDescriptionAttribute) ??
                                         "";

                    _logger.LogDebug($"Found group with SID: {sid}, name: {groupName}");

                    LdapNetGroup group = new LdapNetGroup(
                        sid,
                        groupName,
                        description,
                        entry.DistinguishedName,
                        this
                    );

                    // Cache the group entry
                    CacheGroup(group, ConvertToLdapEntry(entry));

                    return group;
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error finding group by SID: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding group by SID: {ex.Message}");
                return null;
            }
        }

        private LdapNetGroup FindGroupByDn(string groupDn)
        {
            try
            {
                SearchResultEntry entry = GetLdapEntry(groupDn);
                if (entry == null)
                {
                    return null;
                }

                string sid = GetAttributeValue(entry, _config.AttributeMapping.SidAttribute);
                if (string.IsNullOrEmpty(sid))
                {
                    return null;
                }

                string groupName = GetAttributeValue(entry, _config.AttributeMapping.GroupNameAttribute) ??
                                   GetAttributeValue(entry, _config.AttributeMapping.GroupRdnAttribute);
                string description = GetAttributeValue(entry, _config.AttributeMapping.GroupDescriptionAttribute) ?? "";

                LdapNetGroup group = new LdapNetGroup(sid, groupName, description, groupDn, this);

                // Cache the group entry
                CacheGroup(group, ConvertToLdapEntry(entry));

                return group;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding group by DN '{groupDn}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region UserOps

        public override IDirectoryUser<string> CreateUser(DirectoryUserCreationParams parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.Username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(parameters));
            }

            _logger.LogInformation($"Creating user with username: {parameters.Username}");

            // Generate a unique DN for the new user
            string userDn = $"{_config.AttributeMapping.UserRdnAttribute}={parameters.Username},{_config.BaseDn}";

            // Check if user already exists
            if (FindUserByDn(userDn) != null)
            {
                _logger.LogWarning($"User with DN '{userDn}' already exists");
                throw new InvalidOperationException($"User with DN '{userDn}' already exists");
            }

            // Generate a unique identifier (SID equivalent in LDAP)
            string sid = $"L-{Guid.NewGuid().ToString("N")}";

            // Prepare user attributes
            List<DirectoryAttribute> attributes = new List<DirectoryAttribute>
            {
                new DirectoryAttribute("objectClass", "top", "person", "organizationalPerson", "inetOrgPerson"),
                new DirectoryAttribute(_config.AttributeMapping.UserRdnAttribute, parameters.Username),
                new DirectoryAttribute("sn", parameters.Username) // Fallback surname
            };

            // Add mapped attributes based on configuration
            attributes.Add(new DirectoryAttribute(_config.AttributeMapping.UsernameAttribute, parameters.Username));
            attributes.Add(new DirectoryAttribute(_config.AttributeMapping.DisplayNameAttribute,
                parameters.DisplayName ?? parameters.Username));
            attributes.Add(new DirectoryAttribute(_config.AttributeMapping.EmailAttribute, parameters.Email ?? ""));
            attributes.Add(new DirectoryAttribute(_config.AttributeMapping.SidAttribute,
                sid)); // Store our SID equivalent

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Create the new user entry
                    AddRequest addRequest = new AddRequest(userDn, attributes.ToArray());
                    AddResponse addResponse = (AddResponse)connection.SendRequest(addRequest);

                    if (addResponse.ResultCode != ResultCode.Success)
                    {
                        throw new LdapNetProviderException($"Failed to create user: {addResponse.ErrorMessage}");
                    }

                    _logger.LogDebug($"User created with DN: {userDn} and SID: {sid}");

                    return new LdapNetUser(
                        sid,
                        parameters.Username,
                        parameters.DisplayName ?? parameters.Username,
                        parameters.Email,
                        userDn
                    );
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error creating user: {ex.Message}");
                throw new LdapNetProviderException($"Failed to create user: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user: {ex.Message}");
                throw new LdapNetProviderException($"Error creating user: {ex.Message}", ex);
            }
        }

        public override IDirectoryUser<string> FindUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }

            _logger.LogDebug($"Looking up user with username: {username}");

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Search for user with the given username based on configured attributes
                    string userObjectClass = _config.AttributeMapping.UserObjectClass;
                    string usernameAttr = _config.AttributeMapping.UsernameAttribute;
                    string rdnAttr = _config.AttributeMapping.UserRdnAttribute;
                    string sidAttr = _config.AttributeMapping.SidAttribute;

                    // Build filter to search by username using both the configured username attribute and RDN attribute
                    string filter =
                        $"(&(objectClass={userObjectClass})(|({usernameAttr}={username})({rdnAttr}={username}))({sidAttr}=*))";

                    // Prepare list of attributes to retrieve
                    string[] returnAttributes =
                    {
                        sidAttr, rdnAttr, usernameAttr, _config.AttributeMapping.DisplayNameAttribute,
                        _config.AttributeMapping.EmailAttribute
                    };

                    SearchRequest searchRequest = new SearchRequest(
                        _config.BaseDn,
                        filter,
                        SearchScope.Subtree,
                        returnAttributes
                    );

                    SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count == 0)
                    {
                        _logger.LogDebug($"User not found with username: {username}");
                        return null;
                    }

                    // Use the first matching entry
                    SearchResultEntry entry = searchResponse.Entries[0];

                    string sid = GetAttributeValue(entry, _config.AttributeMapping.SidAttribute);
                    string user = GetAttributeValue(entry, _config.AttributeMapping.UsernameAttribute) ??
                                  GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                    string displayName = GetAttributeValue(entry, _config.AttributeMapping.DisplayNameAttribute) ??
                                         GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                    string email = GetAttributeValue(entry, _config.AttributeMapping.EmailAttribute);

                    _logger.LogDebug($"Found user with username: {username}, SID: {sid}");

                    return new LdapNetUser(
                        sid,
                        user,
                        displayName,
                        email,
                        entry.DistinguishedName
                    );
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error finding user: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding user: {ex.Message}");
                return null;
            }
        }

        public override IDirectoryUser<string> GetUserBySid(string sid)
        {
            if (string.IsNullOrEmpty(sid))
            {
                throw new ArgumentNullException(nameof(sid));
            }

            if (!SupportsSidLookup(sid))
            {
                return null;
            }

            _logger.LogDebug($"Looking up user with SID: {sid}");

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Search for user with the given SID using configured attributes
                    string userObjectClass = _config.AttributeMapping.UserObjectClass;
                    string sidAttr = _config.AttributeMapping.SidAttribute;

                    // Build filter to search by SID
                    string filter = $"(&(objectClass={userObjectClass})({sidAttr}={sid}))";

                    SearchRequest searchRequest = new SearchRequest(
                        _config.BaseDn,
                        filter,
                        SearchScope.Subtree,
                        null // Retrieve all attributes
                    );

                    SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count == 0)
                    {
                        _logger.LogDebug($"User not found with SID: {sid}");
                        return null;
                    }

                    // Use the first matching entry
                    SearchResultEntry entry = searchResponse.Entries[0];

                    string username = GetAttributeValue(entry, _config.AttributeMapping.UsernameAttribute) ??
                                      GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                    string displayName = GetAttributeValue(entry, _config.AttributeMapping.DisplayNameAttribute) ??
                                         GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                    string email = GetAttributeValue(entry, _config.AttributeMapping.EmailAttribute);

                    _logger.LogDebug($"Found user with SID: {sid}, username: {username}");

                    return new LdapNetUser(
                        sid,
                        username,
                        displayName,
                        email,
                        entry.DistinguishedName
                    );
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error finding user by SID: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding user by SID: {ex.Message}");
                return null;
            }
        }

        private LdapNetUser FindUserByDn(string userDn)
        {
            try
            {
                SearchResultEntry entry = GetLdapEntry(userDn);
                if (entry == null)
                {
                    return null;
                }

                string sid = GetAttributeValue(entry, _config.AttributeMapping.SidAttribute);
                if (string.IsNullOrEmpty(sid))
                {
                    return null;
                }

                string username = GetAttributeValue(entry, _config.AttributeMapping.UsernameAttribute) ??
                                  GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                string displayName = GetAttributeValue(entry, _config.AttributeMapping.DisplayNameAttribute) ??
                                     GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                string email = GetAttributeValue(entry, _config.AttributeMapping.EmailAttribute);

                return new LdapNetUser(sid, username, displayName, email, userDn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding user by DN '{userDn}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region MembershipOps

        public override void AddMemberToGroup(IDirectoryGroup<string> group, IDirectoryEntity<string> member)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            if (group.ProviderId != ProviderId)
            {
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogInformation($"Adding member with SID '{member.Sid}' to group '{group.GroupName}'");

            // Get the group's DN and the cached entry
            string groupDn = GetDnForGroup(group);
            if (string.IsNullOrEmpty(groupDn))
            {
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found or has no DN");
            }

            // Get the member's DN
            string memberDn;
            if (member.ProviderId == ProviderId)
            {
                // For our own provider, get the DN directly
                memberDn = GetEntityDn(member);
                if (string.IsNullOrEmpty(memberDn))
                {
                    throw new KeyNotFoundException($"Member with SID {member.Sid} not found or has no DN");
                }
            }
            else if (CanHandleForeignEntity(member))
            {
                // For foreign entities we can handle, try to map to a DN
                memberDn = MapForeignEntityToDn(member);
                if (string.IsNullOrEmpty(memberDn))
                {
                    throw new NotSupportedException(
                        $"Cannot map entity from provider {member.ProviderId} to an LDAP DN");
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Provider {ProviderId} cannot handle entities from provider {member.ProviderId}");
            }

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Check if already a member
                    if (IsDirectMember(groupDn, memberDn))
                    {
                        _logger.LogWarning(
                            $"Member with DN '{memberDn}' is already a member of group with DN '{groupDn}'");
                        return;
                    }

                    // Add member to the group by updating the configured member attribute
                    DirectoryAttributeModification memberMod = new DirectoryAttributeModification
                    {
                        Name = _config.AttributeMapping.GroupMemberAttribute,
                        Operation = DirectoryAttributeOperation.Add
                    };
                    memberMod.Add(memberDn);

                    ModifyRequest modifyRequest = new ModifyRequest(groupDn, memberMod);
                    ModifyResponse modifyResponse = (ModifyResponse)connection.SendRequest(modifyRequest);

                    if (modifyResponse.ResultCode != ResultCode.Success)
                    {
                        throw new LdapNetProviderException(
                            $"Failed to add member to group: {modifyResponse.ErrorMessage}");
                    }

                    // Update the cache
                    if (_groupCache.TryGetValue(group.Sid, out LdapEntry cachedEntry))
                    {
                        if (!cachedEntry.Attributes.ContainsKey(_config.AttributeMapping.GroupMemberAttribute))
                        {
                            cachedEntry.Attributes[_config.AttributeMapping.GroupMemberAttribute] = new List<string>();
                        }

                        cachedEntry.Attributes[_config.AttributeMapping.GroupMemberAttribute].Add(memberDn);
                    }

                    _logger.LogDebug($"Successfully added member with DN '{memberDn}' to group with DN '{groupDn}'");
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error adding member to group: {ex.Message}");
                throw new LdapNetProviderException($"Failed to add member to group: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding member to group: {ex.Message}");
                throw new LdapNetProviderException($"Error adding member to group: {ex.Message}", ex);
            }
        }

        public override void RemoveMemberFromGroup(IDirectoryGroup<string> group, IDirectoryEntity<string> member)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            if (group.ProviderId != ProviderId)
            {
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogInformation($"Removing member with SID '{member.Sid}' from group '{group.GroupName}'");

            // Get the group's DN
            string groupDn = GetDnForGroup(group);
            if (string.IsNullOrEmpty(groupDn))
            {
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found or has no DN");
            }

            // Get the member's DN
            string memberDn;
            if (member.ProviderId == ProviderId)
            {
                // For our own provider, get the DN directly
                memberDn = GetEntityDn(member);
                if (string.IsNullOrEmpty(memberDn))
                {
                    throw new KeyNotFoundException($"Member with SID {member.Sid} not found or has no DN");
                }
            }
            else if (CanHandleForeignEntity(member))
            {
                // For foreign entities we can handle, try to map to a DN
                memberDn = MapForeignEntityToDn(member);
                if (string.IsNullOrEmpty(memberDn))
                {
                    // If we can't map it, it's not a member anyway
                    _logger.LogWarning($"Cannot map entity from provider {member.ProviderId} to an LDAP DN");
                    return;
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Provider {ProviderId} cannot handle entities from provider {member.ProviderId}");
            }

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    // Check if actually a member
                    if (!IsDirectMember(groupDn, memberDn))
                    {
                        _logger.LogWarning($"Member with DN '{memberDn}' is not a member of group with DN '{groupDn}'");
                        return;
                    }

                    // Remove member from the group by updating the member attribute
                    DirectoryAttributeModification memberMod = new DirectoryAttributeModification
                    {
                        Name = _config.AttributeMapping.GroupMemberAttribute,
                        Operation = DirectoryAttributeOperation.Delete
                    };
                    memberMod.Add(memberDn);

                    ModifyRequest modifyRequest = new ModifyRequest(groupDn, memberMod);
                    ModifyResponse modifyResponse = (ModifyResponse)connection.SendRequest(modifyRequest);

                    if (modifyResponse.ResultCode != ResultCode.Success)
                    {
                        throw new LdapNetProviderException(
                            $"Failed to remove member from group: {modifyResponse.ErrorMessage}");
                    }

                    // Update the cache
                    if (_groupCache.TryGetValue(group.Sid, out LdapEntry cachedEntry))
                    {
                        if (cachedEntry.Attributes.ContainsKey(_config.AttributeMapping.GroupMemberAttribute))
                        {
                            cachedEntry.Attributes[_config.AttributeMapping.GroupMemberAttribute].Remove(memberDn);
                        }
                    }

                    _logger.LogDebug(
                        $"Successfully removed member with DN '{memberDn}' from group with DN '{groupDn}'");
                }
            }
            catch (DirectoryOperationException ex)
            {
                _logger.LogError(ex, $"LDAP operation error removing member from group: {ex.Message}");
                throw new LdapNetProviderException($"Failed to remove member from group: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing member from group: {ex.Message}");
                throw new LdapNetProviderException($"Error removing member from group: {ex.Message}", ex);
            }
        }

        public override IEnumerable<IDirectoryEntity<string>> GetGroupMembers(IDirectoryGroup<string> group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (group.ProviderId != ProviderId)
            {
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogDebug($"Getting members of group '{group.GroupName}'");

            // Get the group's DN
            string groupDn = GetDnForGroup(group);
            if (string.IsNullOrEmpty(groupDn))
            {
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found or has no DN");
            }

            List<IDirectoryEntity<string>> members = new List<IDirectoryEntity<string>>();
            List<string> memberDns = GetGroupMemberDns(groupDn);

            foreach (string memberDn in memberDns)
            {
                try
                {
                    SearchResultEntry entry = GetLdapEntry(memberDn);
                    if (entry == null)
                    {
                        continue;
                    }

                    // Determine if this is a user or group
                    bool isGroup = false;
                    if (entry.Attributes.Contains(_config.AttributeMapping.ObjectClassAttribute))
                    {
                        object[] objectClasses = entry.Attributes[_config.AttributeMapping.ObjectClassAttribute]
                            .GetValues(typeof(string));
                        isGroup = objectClasses.Cast<string>().Any(c =>
                            string.Equals(c, _config.AttributeMapping.GroupObjectClass,
                                StringComparison.OrdinalIgnoreCase));
                    }

                    if (isGroup)
                    {
                        string sid = GetAttributeValue(entry, _config.AttributeMapping.SidAttribute);
                        if (string.IsNullOrEmpty(sid))
                        {
                            continue;
                        }

                        string groupName = GetAttributeValue(entry, _config.AttributeMapping.GroupNameAttribute) ??
                                           GetAttributeValue(entry, _config.AttributeMapping.GroupRdnAttribute);
                        string description =
                            GetAttributeValue(entry, _config.AttributeMapping.GroupDescriptionAttribute) ?? "";

                        LdapNetGroup nestedGroup = new LdapNetGroup(
                            sid,
                            groupName,
                            description,
                            memberDn,
                            this
                        );

                        // Cache the group
                        CacheGroup(nestedGroup, ConvertToLdapEntry(entry));

                        members.Add(nestedGroup);
                    }
                    else // Assume it's a user
                    {
                        string sid = GetAttributeValue(entry, _config.AttributeMapping.SidAttribute);
                        if (string.IsNullOrEmpty(sid))
                        {
                            continue;
                        }

                        string username = GetAttributeValue(entry, _config.AttributeMapping.UsernameAttribute) ??
                                          GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                        string displayName = GetAttributeValue(entry, _config.AttributeMapping.DisplayNameAttribute) ??
                                             GetAttributeValue(entry, _config.AttributeMapping.UserRdnAttribute);
                        string email = GetAttributeValue(entry, _config.AttributeMapping.EmailAttribute);

                        members.Add(new LdapNetUser(
                            sid,
                            username,
                            displayName,
                            email,
                            memberDn
                        ));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error processing group member with DN '{memberDn}': {ex.Message}");
                }
            }

            _logger.LogDebug($"Found {members.Count} members in group '{group.GroupName}'");
            return members;
        }

        public override bool IsGroupMember(IDirectoryGroup<string> group, IDirectoryEntity<string> entity)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (group.ProviderId != ProviderId)
            {
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogDebug($"Checking if entity with SID '{entity.Sid}' is a member of group '{group.GroupName}'");

            // Get the group's DN
            string groupDn = GetDnForGroup(group);
            if (string.IsNullOrEmpty(groupDn))
            {
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found or has no DN");
            }

            // Get the entity's DN
            string entityDn;
            if (entity.ProviderId == ProviderId)
            {
                entityDn = GetEntityDn(entity);
                if (string.IsNullOrEmpty(entityDn))
                {
                    return false; // If we can't get the DN, it's definitely not a member
                }
            }
            else if (CanHandleForeignEntity(entity))
            {
                entityDn = MapForeignEntityToDn(entity);
                if (string.IsNullOrEmpty(entityDn))
                {
                    return false; // If we can't map it, it's not a member
                }
            }
            else
            {
                return false; // We can't handle this entity type
            }

            // Check direct membership
            if (IsDirectMember(groupDn, entityDn))
            {
                _logger.LogDebug($"Entity with DN '{entityDn}' is a direct member of group with DN '{groupDn}'");
                return true;
            }

            // Check nested groups
            List<string> memberDns = GetGroupMemberDns(groupDn);
            foreach (string memberDn in memberDns)
            {
                try
                {
                    // Check if the member is a group
                    SearchResultEntry entry = GetLdapEntry(memberDn);
                    if (entry == null)
                    {
                        continue;
                    }

                    bool isGroup = false;
                    if (entry.Attributes.Contains(_config.AttributeMapping.ObjectClassAttribute))
                    {
                        object[] objectClasses = entry.Attributes[_config.AttributeMapping.ObjectClassAttribute]
                            .GetValues(typeof(string));
                        isGroup = objectClasses.Cast<string>().Any(c =>
                            string.Equals(c, _config.AttributeMapping.GroupObjectClass,
                                StringComparison.OrdinalIgnoreCase));
                    }

                    if (isGroup)
                    {
                        string nestedGroupSid = GetAttributeValue(entry, _config.AttributeMapping.SidAttribute);
                        if (string.IsNullOrEmpty(nestedGroupSid))
                        {
                            continue;
                        }

                        string nestedGroupName =
                            GetAttributeValue(entry, _config.AttributeMapping.GroupNameAttribute) ??
                            GetAttributeValue(entry, _config.AttributeMapping.GroupRdnAttribute);
                        string description =
                            GetAttributeValue(entry, _config.AttributeMapping.GroupDescriptionAttribute) ?? "";

                        LdapNetGroup nestedGroup = new LdapNetGroup(
                            nestedGroupSid,
                            nestedGroupName,
                            description,
                            memberDn,
                            this
                        );

                        // Cache the group
                        CacheGroup(nestedGroup, ConvertToLdapEntry(entry));

                        // Recursively check nested group
                        if (IsGroupMember(nestedGroup, entity))
                        {
                            _logger.LogDebug(
                                $"Entity with SID '{entity.Sid}' is a member of nested group '{nestedGroupName}'");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error checking nested group membership for DN '{memberDn}': {ex.Message}");
                }
            }

            _logger.LogDebug($"Entity with SID '{entity.Sid}' is not a member of group '{group.GroupName}'");
            return false;
        }

        #endregion

        #region Helper Methods

        private string GetAttributeValue(SearchResultEntry entry, string attributeName)
        {
            if (entry.Attributes.Contains(attributeName))
            {
                return entry.Attributes[attributeName].GetValues(typeof(string))?[0]?.ToString();
            }

            return null;
        }

        private SearchResultEntry GetLdapEntry(string dn)
        {
            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    SearchRequest searchRequest = new SearchRequest(
                        dn,
                        "(objectClass=*)",
                        SearchScope.Base,
                        null // Retrieve all attributes
                    );

                    SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count == 0)
                    {
                        return null;
                    }

                    return searchResponse.Entries[0];
                }
            }
            catch (DirectoryOperationException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting LDAP entry for DN '{dn}': {ex.Message}");
                return null;
            }
        }

        private void CacheGroup(LdapNetGroup group, LdapEntry entry)
        {
            if (group != null && entry != null)
            {
                _groupCache[group.Sid] = entry;
            }
        }

        private string GetDnForGroup(IDirectoryGroup<string> group)
        {
            if (group is LdapNetGroup ldapGroup)
            {
                return ldapGroup.DistinguishedName;
            }

            if (_groupCache.TryGetValue(group.Sid, out LdapEntry cachedEntry))
            {
                return cachedEntry.Dn;
            }

            // Try to look up the group
            LdapNetGroup resolvedGroup = GetGroupBySid(group.Sid) as LdapNetGroup;
            return resolvedGroup?.DistinguishedName;
        }

        private string GetEntityDn(IDirectoryEntity<string> entity)
        {
            if (entity is LdapNetUser user)
            {
                return user.DistinguishedName;
            }

            if (entity is LdapNetGroup group)
            {
                return group.DistinguishedName;
            }

            if (entity is IDirectoryGroup<string> directoryGroup)
            {
                return GetDnForGroup(directoryGroup);
            }

            // For users not in cache, look them up
            if (entity is IDirectoryUser<string>)
            {
                LdapNetUser resolvedUser = GetUserBySid(entity.Sid) as LdapNetUser;
                return resolvedUser?.DistinguishedName;
            }

            return null;
        }

        private bool IsDirectMember(string groupDn, string memberDn)
        {
            List<string> memberDns = GetGroupMemberDns(groupDn);
            return memberDns.Contains(memberDn, StringComparer.OrdinalIgnoreCase);
        }

        private List<string> GetGroupMemberDns(string groupDn)
        {
            List<string> memberDns = new List<string>();

            try
            {
                using (LdapConnection connection = GetConnection())
                {
                    SearchRequest searchRequest = new SearchRequest(
                        groupDn,
                        "(objectClass=*)",
                        SearchScope.Base, _config.AttributeMapping.GroupMemberAttribute);

                    SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count > 0)
                    {
                        SearchResultEntry entry = searchResponse.Entries[0];

                        if (entry.Attributes.Contains(_config.AttributeMapping.GroupMemberAttribute))
                        {
                            DirectoryAttribute memberAttribute =
                                entry.Attributes[_config.AttributeMapping.GroupMemberAttribute];

                            foreach (object value in memberAttribute.GetValues(typeof(string)))
                            {
                                string memberDn = value.ToString();
                                if (!string.IsNullOrWhiteSpace(memberDn))
                                {
                                    memberDns.Add(memberDn);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting member DNs for group '{groupDn}': {ex.Message}");
            }

            return memberDns;
        }

        private LdapEntry ConvertToLdapEntry(SearchResultEntry entry)
        {
            LdapEntry ldapEntry = new LdapEntry
            {
                Dn = entry.DistinguishedName, Attributes = new Dictionary<string, List<string>>()
            };

            foreach (DirectoryAttribute attr in entry.Attributes.Values)
            {
                ldapEntry.Attributes[attr.Name] = new List<string>();
                foreach (object val in attr.GetValues(typeof(string)))
                {
                    ldapEntry.Attributes[attr.Name].Add(val.ToString());
                }
            }

            return ldapEntry;
        }

        private string MapForeignEntityToDn(IDirectoryEntity<string> entity)
        {
            // This would be a custom implementation depending on your environment
            // Here's a simple example that could be expanded based on your needs

            if (entity.ProviderId == "ActiveDirectory" || entity.ProviderId == "WindowsLocal")
            {
                // Look for corresponding entries in LDAP directory based on SID mappings
                // This is highly dependent on your specific directory setup

                try
                {
                    using (LdapConnection connection = GetConnection())
                    {
                        // Search for entries that have a custom attribute mapping to the Windows SID
                        // For example, you might have a custom attribute like "windowsSid" that stores
                        // the SID from Active Directory
                        SearchRequest searchRequest = new SearchRequest(
                            _config.BaseDn,
                            $"(windowsSid={entity.Sid})",
                            SearchScope.Subtree,
                            null
                        );

                        SearchResponse searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                        if (searchResponse.Entries.Count > 0)
                        {
                            return searchResponse.Entries[0].DistinguishedName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error mapping foreign entity to DN: {ex.Message}");
                }
            }

            else if (entity.ProviderId == "JsonFile")
            {
                // Similar approach for JSON file provider entities
                // You might have a different attribute mapping for these
            }

            return null;
        }

        #endregion
    }
}