using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.QuickFolderLinks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        [JsonProperty]
        public int MaxResult { get; set; } = 100;

        [JsonProperty]
        public List<FolderLink> FolderLinks { get; set; } = new List<FolderLink>();

        [JsonProperty]
        public bool UseWindowsIndexForDirectorySearch { get; set; } = true;

        [JsonProperty]
        public List<string> IndexSearchExcludedDirectories { get; set; } = new List<string>();
    }
}
