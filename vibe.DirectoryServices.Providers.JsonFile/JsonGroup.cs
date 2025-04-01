using System;

namespace vibe.DirectoryServices.Providers.JsonFile
{
    public class JsonGroup : BaseDirectoryGroup<string>
    {
        public JsonGroup(string sid, string groupName, string description,
            DateTime createdAt, DateTime? lastModified = null, IDirectoryProvider<string> provider = null)
            : base(sid, groupName, "JsonFile", provider)
        {
            Description = description;
            CreatedAt = createdAt;
            LastModified = lastModified;
        }

        public string Description { get; }
        public DateTime CreatedAt { get; }
        public DateTime? LastModified { get; }
    }
}