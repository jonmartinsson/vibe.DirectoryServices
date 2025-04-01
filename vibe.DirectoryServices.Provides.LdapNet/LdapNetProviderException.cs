using System;

namespace vibe.DirectoryServices.Providers.LdapNet
{
    public class LdapNetProviderException : Exception
    {
        public LdapNetProviderException(string message) : base(message)
        {
        }

        public LdapNetProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
