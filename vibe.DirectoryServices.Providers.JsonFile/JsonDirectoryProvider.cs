using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace vibe.DirectoryServices.Providers.JsonFile
{
    public class JsonDirectoryProvider : DirectoryProviderBase<string>
    {
        private readonly SemaphoreSlim _fileAccessLock = new SemaphoreSlim(1, 1);
        private readonly string _filePath;
        private JsonDirectoryData _directoryData;

        public JsonDirectoryProvider(JsonDirectoryProviderConfiguration configuration, ILogger<JsonDirectoryProvider> logger) : base(logger)
        {
            _filePath = configuration.FilePath ?? throw new ArgumentNullException(nameof(configuration.FilePath));
            _logger.LogInformation($"Initializing JSON File provider with file: {_filePath}");

            try
            {
                InitializeStorage();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize JSON File provider", ex);
                throw new JsonProviderException("Failed to initialize storage", ex);
            }
        }

        public override string ProviderId => "JsonFile";

        public override bool CanHandleForeignEntity(IDirectoryEntity<string> entity)
        {
            // JSON provider can handle entities from any provider
            return true;
        }

        public override IDirectoryUser<string> CreateUser(DirectoryUserCreationParams parameters)
        {
            if (parameters == null)
            {
                _logger.LogWarning("CreateUser called with null parameters");
                throw new ArgumentNullException(nameof(parameters));
            }

            if (string.IsNullOrWhiteSpace(parameters.Username))
            {
                _logger.LogWarning("CreateUser called with empty username");
                throw new ArgumentException("Username cannot be empty", nameof(parameters));
            }

            _logger.LogInformation($"Creating user with username: {parameters.Username}");

            // Check if user already exists
            if (_directoryData.Users.Any(u =>
                    string.Equals(u.Username, parameters.Username, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning($"User with username '{parameters.Username}' already exists");
                throw new InvalidOperationException($"User with username '{parameters.Username}' already exists");
            }

            string sid = GenerateSid();
            DateTime timestamp = DateTime.UtcNow;

            JsonDirectoryData.JsonUserData userData = new JsonDirectoryData.JsonUserData
            {
                Sid = sid,
                Username = parameters.Username,
                DisplayName = parameters.DisplayName ?? parameters.Username,
                Email = parameters.Email,
                CreatedAt = timestamp,
                LastModified = null
            };

            _directoryData.Users.Add(userData);
            SaveChanges();

            _logger.LogDebug($"User created with SID: {sid}");

            return new JsonUser(
                sid,
                parameters.Username,
                parameters.DisplayName ?? parameters.Username,
                parameters.Email,
                timestamp
            );
        }

        public override IDirectoryGroup<string> CreateGroup(DirectoryGroupCreationParams parameters)
        {
            if (parameters == null)
            {
                _logger.LogWarning("CreateGroup called with null parameters");
                throw new ArgumentNullException(nameof(parameters));
            }

            if (string.IsNullOrWhiteSpace(parameters.GroupName))
            {
                _logger.LogWarning("CreateGroup called with empty group name");
                throw new ArgumentException("Group name cannot be empty", nameof(parameters));
            }

            _logger.LogInformation($"Creating group with name: {parameters.GroupName}");

            // Check if group already exists
            if (_directoryData.Groups.Any(g =>
                    string.Equals(g.GroupName, parameters.GroupName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning($"Group with name '{parameters.GroupName}' already exists");
                throw new InvalidOperationException($"Group with name '{parameters.GroupName}' already exists");
            }

            string sid = GenerateSid();
            DateTime timestamp = DateTime.UtcNow;

            JsonDirectoryData.JsonGroupData groupData = new JsonDirectoryData.JsonGroupData
            {
                Sid = sid,
                GroupName = parameters.GroupName,
                Description = parameters.Description ?? "",
                CreatedAt = timestamp,
                LastModified = null,
                Members = new List<JsonDirectoryData.JsonMemberData>()
            };

            _directoryData.Groups.Add(groupData);
            SaveChanges();

            _logger.LogDebug($"Group created with SID: {sid}");

            return new JsonGroup(
                sid,
                parameters.GroupName,
                parameters.Description ?? "",
                timestamp,
                provider: this
            );
        }

        public override IDirectoryUser<string> FindUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("FindUser called with empty username");
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }

            _logger.LogDebug($"Looking up user with username: {username}");

            JsonDirectoryData.JsonUserData userData = _directoryData.Users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

            if (userData == null)
            {
                _logger.LogDebug($"User not found with username: {username}");
                return null;
            }

            _logger.LogDebug($"Found user with username: {username}, SID: {userData.Sid}");

            return new JsonUser(
                userData.Sid,
                userData.Username,
                userData.DisplayName,
                userData.Email,
                userData.CreatedAt,
                userData.LastModified
            );
        }

        public override IDirectoryGroup<string> FindGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                _logger.LogWarning("FindGroup called with empty group name");
                throw new ArgumentException("Group name cannot be empty", nameof(groupName));
            }

            _logger.LogDebug($"Looking up group with name: {groupName}");

            JsonDirectoryData.JsonGroupData groupData = _directoryData.Groups.FirstOrDefault(g =>
                string.Equals(g.GroupName, groupName, StringComparison.OrdinalIgnoreCase));

            if (groupData == null)
            {
                _logger.LogDebug($"Group not found with name: {groupName}");
                return null;
            }

            _logger.LogDebug($"Found group with name: {groupName}, SID: {groupData.Sid}");

            return new JsonGroup(
                groupData.Sid,
                groupData.GroupName,
                groupData.Description,
                groupData.CreatedAt,
                groupData.LastModified,
                this
            );
        }

        public override void AddMemberToGroup(IDirectoryGroup<string> group, IDirectoryEntity<string> member)
        {
            if (group == null)
            {
                _logger.LogWarning("AddMemberToGroup called with null group");
                throw new ArgumentNullException(nameof(group));
            }

            if (member == null)
            {
                _logger.LogWarning("AddMemberToGroup called with null member");
                throw new ArgumentNullException(nameof(member));
            }

            if (group.ProviderId != ProviderId)
            {
                _logger.LogWarning(
                    $"AddMemberToGroup called with group from provider '{group.ProviderId}', expected '{ProviderId}'");
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogInformation($"Adding member with SID '{member.Sid}' to group '{group.GroupName}'");

            JsonDirectoryData.JsonGroupData groupData = _directoryData.Groups.FirstOrDefault(g => g.Sid == group.Sid);
            if (groupData == null)
            {
                _logger.LogWarning($"Group with SID '{group.Sid}' not found");
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found");
            }

            if (groupData.Members.Any(m => m.Sid == member.Sid && m.ProviderId == member.ProviderId))
            {
                _logger.LogWarning($"Member with SID '{member.Sid}' is already a member of group '{group.GroupName}'");
                return;
            }

            JsonDirectoryData.JsonMemberData memberData = new JsonDirectoryData.JsonMemberData
            {
                Sid = member.Sid,
                ProviderId = member.ProviderId,
                MemberType = member is IDirectoryUser<string> ? "User" : "Group"
            };

            groupData.Members.Add(memberData);
            groupData.LastModified = DateTime.UtcNow;
            SaveChanges();

            _logger.LogDebug($"Successfully added member with SID '{member.Sid}' to group '{group.GroupName}'");
        }

        public override void RemoveMemberFromGroup(IDirectoryGroup<string> group, IDirectoryEntity<string> member)
        {
            if (group == null)
            {
                _logger.LogWarning("RemoveMemberFromGroup called with null group");
                throw new ArgumentNullException(nameof(group));
            }

            if (member == null)
            {
                _logger.LogWarning("RemoveMemberFromGroup called with null member");
                throw new ArgumentNullException(nameof(member));
            }

            if (group.ProviderId != ProviderId)
            {
                _logger.LogWarning(
                    $"RemoveMemberFromGroup called with group from provider '{group.ProviderId}', expected '{ProviderId}'");
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogInformation($"Removing member with SID '{member.Sid}' from group '{group.GroupName}'");

            JsonDirectoryData.JsonGroupData groupData = _directoryData.Groups.FirstOrDefault(g => g.Sid == group.Sid);
            if (groupData == null)
            {
                _logger.LogWarning($"Group with SID '{group.Sid}' not found");
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found");
            }

            JsonDirectoryData.JsonMemberData memberData = groupData.Members.FirstOrDefault(m =>
                m.Sid == member.Sid && m.ProviderId == member.ProviderId);

            if (memberData != null)
            {
                groupData.Members.Remove(memberData);
                groupData.LastModified = DateTime.UtcNow;
                SaveChanges();
                _logger.LogDebug($"Successfully removed member with SID '{member.Sid}' from group '{group.GroupName}'");
            }
            else
            {
                _logger.LogWarning($"Member with SID '{member.Sid}' is not a member of group '{group.GroupName}'");
            }
        }

        public override IEnumerable<IDirectoryEntity<string>> GetGroupMembers(IDirectoryGroup<string> group)
        {
            if (group == null)
            {
                _logger.LogWarning("GetGroupMembers called with null group");
                throw new ArgumentNullException(nameof(group));
            }

            if (group.ProviderId != ProviderId)
            {
                _logger.LogWarning(
                    $"GetGroupMembers called with group from provider '{group.ProviderId}', expected '{ProviderId}'");
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogDebug($"Getting members of group '{group.GroupName}'");

            JsonDirectoryData.JsonGroupData groupData = _directoryData.Groups.FirstOrDefault(g => g.Sid == group.Sid);
            if (groupData == null)
            {
                _logger.LogWarning($"Group with SID '{group.Sid}' not found");
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found");
            }

            List<IDirectoryEntity<string>> members = new List<IDirectoryEntity<string>>();

            foreach (JsonDirectoryData.JsonMemberData memberData in groupData.Members)
            {
                if (memberData.ProviderId == ProviderId)
                {
                    if (memberData.MemberType == "User")
                    {
                        JsonDirectoryData.JsonUserData userData =
                            _directoryData.Users.FirstOrDefault(u => u.Sid == memberData.Sid);
                        if (userData != null)
                        {
                            members.Add(new JsonUser(
                                userData.Sid,
                                userData.Username,
                                userData.DisplayName,
                                userData.Email,
                                userData.CreatedAt,
                                userData.LastModified
                            ));
                        }
                    }
                    else // Group
                    {
                        JsonDirectoryData.JsonGroupData nestedGroupData =
                            _directoryData.Groups.FirstOrDefault(g => g.Sid == memberData.Sid);
                        if (nestedGroupData != null)
                        {
                            members.Add(new JsonGroup(
                                nestedGroupData.Sid,
                                nestedGroupData.GroupName,
                                nestedGroupData.Description,
                                nestedGroupData.CreatedAt,
                                nestedGroupData.LastModified,
                                this
                            ));
                        }
                    }
                }
            }

            _logger.LogDebug($"Found {members.Count} members in group '{group.GroupName}'");
            return members;
        }

        public override bool IsGroupMember(IDirectoryGroup<string> group, IDirectoryEntity<string> entity)
        {
            if (group == null)
            {
                _logger.LogWarning("IsGroupMember called with null group");
                throw new ArgumentNullException(nameof(group));
            }

            if (entity == null)
            {
                _logger.LogWarning("IsGroupMember called with null entity");
                throw new ArgumentNullException(nameof(entity));
            }

            if (group.ProviderId != ProviderId)
            {
                _logger.LogWarning(
                    $"IsGroupMember called with group from provider '{group.ProviderId}', expected '{ProviderId}'");
                throw new ArgumentException($"Group is not from the {ProviderId} provider", nameof(group));
            }

            _logger.LogDebug($"Checking if entity with SID '{entity.Sid}' is a member of group '{group.GroupName}'");

            JsonDirectoryData.JsonGroupData groupData = _directoryData.Groups.FirstOrDefault(g => g.Sid == group.Sid);
            if (groupData == null)
            {
                _logger.LogWarning($"Group with SID '{group.Sid}' not found");
                throw new KeyNotFoundException($"Group with SID {group.Sid} not found");
            }

            bool isDirectMember = groupData.Members.Any(m =>
                m.Sid == entity.Sid && m.ProviderId == entity.ProviderId);

            if (isDirectMember)
            {
                _logger.LogDebug($"Entity with SID '{entity.Sid}' is a direct member of group '{group.GroupName}'");
                return true;
            }

            // Check nested groups
            foreach (JsonDirectoryData.JsonMemberData memberData in groupData.Members.Where(m =>
                         m.MemberType == "Group" && m.ProviderId == ProviderId))
            {
                JsonDirectoryData.JsonGroupData nestedGroupData =
                    _directoryData.Groups.FirstOrDefault(g => g.Sid == memberData.Sid);
                if (nestedGroupData != null)
                {
                    JsonGroup nestedGroup = new JsonGroup(
                        nestedGroupData.Sid,
                        nestedGroupData.GroupName,
                        nestedGroupData.Description,
                        nestedGroupData.CreatedAt,
                        nestedGroupData.LastModified,
                        this
                    );

                    if (IsGroupMember(nestedGroup, entity))
                    {
                        _logger.LogDebug(
                            $"Entity with SID '{entity.Sid}' is a member of nested group '{nestedGroup.GroupName}'");
                        return true;
                    }
                }
            }

            _logger.LogDebug($"Entity with SID '{entity.Sid}' is not a member of group '{group.GroupName}'");
            return false;
        }

        public override bool SupportsSidLookup(string sid)
        {
            // JSON provider can handle SIDs that start with "J-"
            return sid != null && sid.StartsWith("J-");
        }

        public override IDirectoryUser<string> GetUserBySid(string sid)
        {
            if (sid == null)
            {
                _logger.LogWarning("GetUserBySid called with null SID");
                throw new ArgumentNullException(nameof(sid));
            }

            if (!SupportsSidLookup(sid))
            {
                _logger.LogDebug($"SID format '{sid}' not supported by JSON provider");
                return null;
            }

            _logger.LogDebug($"Looking up user with SID: {sid}");

            JsonDirectoryData.JsonUserData userData = _directoryData.Users.FirstOrDefault(u => u.Sid == sid);
            if (userData == null)
            {
                _logger.LogDebug($"User not found with SID: {sid}");
                return null;
            }

            _logger.LogDebug($"Found user with SID: {sid}, username: {userData.Username}");

            return new JsonUser(
                userData.Sid,
                userData.Username,
                userData.DisplayName,
                userData.Email,
                userData.CreatedAt,
                userData.LastModified
            );
        }

        public override IDirectoryGroup<string> GetGroupBySid(string sid)
        {
            if (sid == null)
            {
                _logger.LogWarning("GetGroupBySid called with null SID");
                throw new ArgumentNullException(nameof(sid));
            }

            if (!SupportsSidLookup(sid))
            {
                _logger.LogDebug($"SID format '{sid}' not supported by JSON provider");
                return null;
            }

            _logger.LogDebug($"Looking up group with SID: {sid}");

            JsonDirectoryData.JsonGroupData groupData = _directoryData.Groups.FirstOrDefault(g => g.Sid == sid);
            if (groupData == null)
            {
                _logger.LogDebug($"Group not found with SID: {sid}");
                return null;
            }

            _logger.LogDebug($"Found group with SID: {sid}, name: {groupData.GroupName}");

            return new JsonGroup(
                groupData.Sid,
                groupData.GroupName,
                groupData.Description,
                groupData.CreatedAt,
                groupData.LastModified,
                this
            );
        }

        private void InitializeStorage()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    _logger.LogDebug($"Loading data from existing file: {_filePath}");
                    string jsonContent = File.ReadAllText(_filePath);
                    _directoryData = JsonSerializer.Deserialize<JsonDirectoryData>(jsonContent)
                                     ?? new JsonDirectoryData();

                    _logger.LogInformation(
                        $"Loaded {_directoryData.Users.Count} users and {_directoryData.Groups.Count} groups from JSON file");
                }
                else
                {
                    _logger.LogDebug($"JSON file not found at {_filePath}, creating new data structure");
                    _directoryData = new JsonDirectoryData();
                    SaveChanges();
                    _logger.LogInformation("Created new empty directory data structure");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Error parsing JSON file: {_filePath}", ex);
                throw new JsonProviderException($"Error parsing JSON file: {_filePath}", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"IO error accessing file: {_filePath}", ex);
                throw new JsonProviderException($"IO error accessing file: {_filePath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error initializing JSON storage", ex);
                throw new JsonProviderException("Unexpected error initializing JSON storage", ex);
            }
        }

        private async void SaveChanges()
        {
            try
            {
                await _fileAccessLock.WaitAsync();
                _logger.LogDebug("Saving changes to JSON file");

                string jsonContent =
                    JsonSerializer.Serialize(_directoryData, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(_filePath, jsonContent);
                _logger.LogDebug("Successfully saved changes to JSON file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving JSON file: {_filePath}", ex);
            }
            finally
            {
                _fileAccessLock.Release();
            }
        }

        private string GenerateSid()
        {
            return $"J-{Guid.NewGuid().ToString("N")}";
        }
    }
}