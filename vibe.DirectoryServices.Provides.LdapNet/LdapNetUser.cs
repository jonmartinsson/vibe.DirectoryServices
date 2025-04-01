namespace vibe.DirectoryServices.Providers.LdapNet
{
    public class LdapNetUser : BaseDirectoryUser<string>
    {
        public LdapNetUser(string sid, string username, string displayName, string email, string distinguishedName)
            : base(sid, username, displayName, "LdapNet")
        {
            Email = email;
            DistinguishedName = distinguishedName;
        }

        public string Email { get; }
        public string DistinguishedName { get; }
    }
}
