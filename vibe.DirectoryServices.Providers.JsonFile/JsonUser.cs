using System;

namespace vibe.DirectoryServices.Providers.JsonFile
{
    public class JsonUser : BaseDirectoryUser<string>
    {
        public JsonUser(string sid, string username, string displayName, string email,
            DateTime createdAt, DateTime? lastModified = null)
            : base(sid, username, displayName, "JsonFile")
        {
            Email = email;
            CreatedAt = createdAt;
            LastModified = lastModified;
        }

        public string Email { get; }
        public DateTime CreatedAt { get; }
        public DateTime? LastModified { get; }
    }
}