namespace vibe.DirectoryServices
{
    public interface IDirectoryUser<out TSid> : IDirectoryEntity<TSid>
    {
        string Username { get; }
        string DisplayName { get; }
    }
}