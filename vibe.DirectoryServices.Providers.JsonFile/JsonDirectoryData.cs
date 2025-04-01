using System;
using System.Collections.Generic;

namespace vibe.DirectoryServices.Providers.JsonFile
{
    public class JsonDirectoryData
    {
        public List<JsonUserData> Users { get; set; } = new List<JsonUserData>();
        public List<JsonGroupData> Groups { get; set; } = new List<JsonGroupData>();

        public class JsonUserData
        {
            public string Sid { get; set; }
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string Email { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastModified { get; set; }
        }

        public class JsonGroupData
        {
            public string Sid { get; set; }
            public string GroupName { get; set; }
            public string Description { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastModified { get; set; }
            public List<JsonMemberData> Members { get; set; } = new List<JsonMemberData>();
        }

        public class JsonMemberData
        {
            public string Sid { get; set; }
            public string ProviderId { get; set; }
            public string MemberType { get; set; } // "User" or "Group"
        }
    }
}