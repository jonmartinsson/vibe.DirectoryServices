using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace vibe.DirectoryServices
{
    /// <summary>
    /// Base class for directory providers that implements common functionality
    /// </summary>
    /// <typeparam name="TSid">The type of security identifier used by the provider</typeparam>
    public abstract class DirectoryProviderBase<TSid> : IDirectoryProvider<TSid>
    {
        protected readonly ILogger _logger;

        protected DirectoryProviderBase(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string ProviderId { get; }

        public abstract IDirectoryUser<TSid> CreateUser(DirectoryUserCreationParams parameters);
        public abstract IDirectoryGroup<TSid> CreateGroup(DirectoryGroupCreationParams parameters);
        public abstract IDirectoryUser<TSid> FindUser(string username);
        public abstract IDirectoryGroup<TSid> FindGroup(string groupName);
        
        public abstract void AddMemberToGroup(IDirectoryGroup<TSid> group, IDirectoryEntity<TSid> member);
        public abstract void RemoveMemberFromGroup(IDirectoryGroup<TSid> group, IDirectoryEntity<TSid> member);
        public abstract IEnumerable<IDirectoryEntity<TSid>> GetGroupMembers(IDirectoryGroup<TSid> group);
        public abstract bool IsGroupMember(IDirectoryGroup<TSid> group, IDirectoryEntity<TSid> entity);
        
        public virtual bool CanHandleForeignEntity(IDirectoryEntity<TSid> entity)
        {
            // By default, providers only handle their own entities
            return entity.ProviderId == ProviderId;
        }
        
        public abstract bool SupportsSidLookup(TSid sid);
        public abstract IDirectoryUser<TSid> GetUserBySid(TSid sid);
        public abstract IDirectoryGroup<TSid> GetGroupBySid(TSid sid);
    }
}
