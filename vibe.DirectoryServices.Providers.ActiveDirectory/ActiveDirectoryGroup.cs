namespace vibe.DirectoryServices.Providers.Adsi
{
    public class ActiveDirectoryGroup : BaseDirectoryGroup<string>
    {
        public ActiveDirectoryGroup(string sid, string groupName, string distinguishedName,
            IDirectoryProvider<string> provider)
            : base(sid, groupName, "ActiveDirectory", provider)
        {
            DistinguishedName = distinguishedName;
        }

        public string DistinguishedName { get; }
    }
}