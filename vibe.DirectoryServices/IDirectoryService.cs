using System.Collections.Generic;

namespace vibe.DirectoryServices
{
    public interface IDirectoryService<TSid>
    {
        IEnumerable<IDirectoryUser<TSid>> SearchUsers(string searchTerm, UserSearchType searchType);
        IDirectoryUser<TSid> GetUserById(TSid sid);
        IDirectoryGroup<TSid> GetGroupById(TSid sid);
    }
}