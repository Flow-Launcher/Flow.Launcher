using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.FolderLinks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        [JsonProperty]
        public int MaxResult { get; set; } = 100;

        [JsonProperty]
        public List<FolderLink> QuickFolderAccessLinks { get; set; } = new List<FolderLink>();

        [JsonProperty]
        public bool UseWindowsIndexForDirectorySearch { get; set; } = true;

        [JsonProperty]
        public List<FolderLink> IndexSearchExcludedSubdirectoryPaths { get; set; } = new List<FolderLink>();

        [JsonProperty]
        public string SearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        [JsonProperty]
        public string FileContentSearchActionKeyword { get; set; } = Constants.DefaultContentSearchActionKeyword;
    }
}