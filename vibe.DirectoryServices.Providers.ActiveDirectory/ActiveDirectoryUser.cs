namespace vibe.DirectoryServices.Providers.Adsi
{

    public class ActiveDirectoryUser : BaseDirectoryUser<string>
    {
        public string DistinguishedName { get; }

        public ActiveDirectoryUser(string sid, string username, string displayName, string distinguishedName)
            : base(sid, username, displayName, "ActiveDirectory")
        {
            DistinguishedName = distinguishedName;
        }
    }

}