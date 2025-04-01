using System.DirectoryServices.Protocols;

namespace vibe.DirectoryServices.Providers.LdapNet
{
    public class LdapNetDirectoryProviderConfiguration
    {
        public string ServerAddress { get; set; }
        public int ServerPort { get; set; } = 389;
        public string BaseDn { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; } = false;
        public AuthType AuthType { get; set; } = AuthType.Basic;
        public LdapAttributeMapping AttributeMapping { get; set; } = new LdapAttributeMapping();
    }
}