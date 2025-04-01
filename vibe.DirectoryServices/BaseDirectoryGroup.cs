using System;
using System.Collections.Generic;

namespace vibe.DirectoryServices
{
    public abstract class BaseDirectoryGroup<TSid> : IDirectoryGroup<TSid>
    {
        private readonly IDirectoryProvider<TSid> _provider;

        protected BaseDirectoryGroup(TSid sid, string groupName, string providerId, IDirectoryProvider<TSid> provider)
        {
            Sid = sid;
            GroupName = groupName;
            ProviderId = providerId;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public TSid Sid { get; protected set; }
        public string GroupName { get; protected set; }
        public string ProviderId { get; protected set; }

        public void AddMember(IDirectoryEntity<TSid> member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            _provider.AddMemberToGroup(this, member);
        }

        public void RemoveMember(IDirectoryEntity<TSid> member)
        {
            _provider.RemoveMemberFromGroup(this, member);
        }

        public bool IsMember(IDirectoryEntity<TSid> entity)
        {
            return _provider.IsGroupMember(this, entity);
        }

        public IEnumerable<IDirectoryEntity<TSid>> GetMembers()
        {
            return _provider.GetGroupMembers(this);
        }
    }
}