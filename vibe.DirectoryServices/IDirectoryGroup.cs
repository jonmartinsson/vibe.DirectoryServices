using System.Collections.Generic;

namespace vibe.DirectoryServices
{
    public interface IDirectoryGroup<TSid> : IDirectoryEntity<TSid>
    {
        string GroupName { get; }

        void AddMember(IDirectoryEntity<TSid> member);
        void RemoveMember(IDirectoryEntity<TSid> member);
        IEnumerable<IDirectoryEntity<TSid>> GetMembers();
        bool IsMember(IDirectoryEntity<TSid> entity);
    }
}