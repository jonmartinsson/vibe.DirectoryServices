using System;

namespace vibe.DirectoryServices
{
    public class DirectoryServiceException : Exception
    {
        public DirectoryServiceException(string message) : base(message)
        {
        }

        public DirectoryServiceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public DirectoryServiceException(string providerId, string message, Exception innerException)
            : base($"Provider '{providerId}': {message}", innerException)
        {
            ProviderId = providerId;
        }

        public string ProviderId { get; }
    }
}