namespace vibe.DirectoryServices.Providers.Adsi
{
    public class LocalWindowsUser : BaseDirectoryUser<string>
    {
        public LocalWindowsUser(string sid, string username, string displayName, string accountName)
            : base(sid, username, displayName, "WindowsLocal")
        {
            AccountName = accountName;
        }

        public string AccountName { get; }
    }
}