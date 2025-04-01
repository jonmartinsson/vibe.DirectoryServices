using System.Collections.Generic;

namespace vibe.DirectoryServices
{
    public interface IDirectoryProvider<TSid>
    {
        string ProviderId { get; }

        IDirectoryUser<TSid> CreateUser(DirectoryUserCreationParams parameters);
        IDirectoryGroup<TSid> CreateGroup(DirectoryGroupCreationParams parameters);
        IDirectoryUser<TSid> FindUser(string username);
        IDirectoryGroup<TSid> FindGroup(string groupName);
        void AddMemberToGroup(IDirectoryGroup<TSid> group, IDirectoryEntity<TSid> member);
        void RemoveMemberFromGroup(IDirectoryGroup<TSid> group, IDirectoryEntity<TSid> member);
        IEnumerable<IDirectoryEntity<TSid>> GetGroupMembers(IDirectoryGroup<TSid> group);

        bool IsGroupMember(IDirectoryGroup<TSid> group, IDirectoryEntity<TSid> entity);

        // Methods for cross-provider operation
        bool CanHandleForeignEntity(IDirectoryEntity<TSid> entity);

        // Methods for entity lookup by ID
        bool SupportsSidLookup(TSid sid);
        IDirectoryUser<TSid> GetUserBySid(TSid sid);
        IDirectoryGroup<TSid> GetGroupBySid(TSid sid);
    }
}