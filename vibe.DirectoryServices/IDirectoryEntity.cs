namespace vibe.DirectoryServices
{
    public interface IDirectoryEntity<out TSid>
    {
        TSid Sid { get; }
        string ProviderId { get; }
    }
}