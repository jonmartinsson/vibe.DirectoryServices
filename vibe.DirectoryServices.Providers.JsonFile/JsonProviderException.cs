using System;

namespace vibe.DirectoryServices.Providers.JsonFile
{
    public class JsonProviderException : Exception
    {
        public JsonProviderException(string message) : base(message)
        {
        }

        public JsonProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}