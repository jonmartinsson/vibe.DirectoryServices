using System.Collections.Generic;

namespace ConsoleTester
{
    public class ProviderSelections
    {
        public bool UseJson { get; set; }
        public bool UseWindowsLocal { get; set; }
        public bool UseActiveDirectory { get; set; }
        public bool UseLdapNet { get; set; }

        public bool IsMultipleProvidersSelected =>
            (UseJson ? 1 : 0) + (UseWindowsLocal ? 1 : 0) + (UseActiveDirectory ? 1 : 0) + (UseLdapNet ? 1 : 0) > 1;

        public List<string> GetSelectedProviders()
        {
            List<string> selected = new List<string>();
            if (UseJson)
            {
                selected.Add("JsonFile");
            }

            if (UseWindowsLocal)
            {
                selected.Add("WindowsLocal");
            }

            if (UseActiveDirectory)
            {
                selected.Add("ActiveDirectory");
            }

            if (UseLdapNet)
            {
                selected.Add("LdapNet");
            }

            return selected;
        }
    }
}