namespace vibe.DirectoryServices
{
    public abstract class BaseDirectoryUser<TSid> : IDirectoryUser<TSid>
    {
        protected BaseDirectoryUser(TSid sid, string username, string displayName, string providerId)
        {
            Sid = sid;
            Username = username;
            DisplayName = displayName;
            ProviderId = providerId;
        }

        public TSid Sid { get; protected set; }
        public string Username { get; protected set; }
        public string DisplayName { get; protected set; }
        public string ProviderId { get; protected set; }
    }
}