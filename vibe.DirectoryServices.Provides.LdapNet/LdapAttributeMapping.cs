namespace vibe.DirectoryServices.Providers.LdapNet
{
    public class LdapAttributeMapping
    {
        public string UsernameAttribute { get; set; } = "uid";
        public string DisplayNameAttribute { get; set; } = "displayName";
        public string EmailAttribute { get; set; } = "mail";
        public string SidAttribute { get; set; } = "entryUUID";
        public string GroupNameAttribute { get; set; } = "cn";
        public string GroupDescriptionAttribute { get; set; } = "description";
        public string GroupMemberAttribute { get; set; } = "member";
        public string ObjectClassAttribute { get; set; } = "objectClass";
        public string UserObjectClass { get; set; } = "person";
        public string GroupObjectClass { get; set; } = "groupOfNames";
        public string UserRdnAttribute { get; set; } = "cn"; // Attribute used in DN for new users
        public string GroupRdnAttribute { get; set; } = "cn"; // Attribute used in DN for new groups
    }
}
