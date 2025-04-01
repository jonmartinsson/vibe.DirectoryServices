namespace vibe.DirectoryServices.Providers.Adsi
{
    public class LocalWindowsGroup : BaseDirectoryGroup<string>
    {
        public LocalWindowsGroup(string sid, string groupName, string accountName, IDirectoryProvider<string> provider)
            : base(sid, groupName, "WindowsLocal", provider)
        {
            AccountName = accountName;
        }

        public string AccountName { get; }
    }
}