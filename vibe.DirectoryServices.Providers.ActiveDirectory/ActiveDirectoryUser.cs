namespace vibe.DirectoryServices.Providers.Adsi
{
    public class ActiveDirectoryUser : BaseDirectoryUser<string>
    {
        public ActiveDirectoryUser(string sid, string username, string displayName, string distinguishedName)
            : base(sid, username, displayName, "ActiveDirectory")
        {
            DistinguishedName = distinguishedName;
        }

        public string DistinguishedName { get; }
    }
}