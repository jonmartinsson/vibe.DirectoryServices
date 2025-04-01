namespace vibe.DirectoryServices.Providers.LdapNet
{
    public class LdapNetGroup : BaseDirectoryGroup<string>
    {
        public LdapNetGroup(string sid, string groupName, string description, string distinguishedName, IDirectoryProvider<string> provider)
            : base(sid, groupName, "LdapNet", provider)
        {
            Description = description;
            DistinguishedName = distinguishedName;
        }

        public string Description { get; }
        public string DistinguishedName { get; }
    }
}
