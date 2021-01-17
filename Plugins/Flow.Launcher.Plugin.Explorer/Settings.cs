using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.FolderLinks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;

        public List<FolderLink> QuickFolderAccessLinks { get; set; } = new List<FolderLink>();

        public bool UseWindowsIndexForDirectorySearch { get; set; } = true;

        public List<FolderLink> IndexSearchExcludedSubdirectoryPaths { get; set; } = new List<FolderLink>();

        public string SearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public string FileContentSearchActionKeyword { get; set; } = Constants.DefaultContentSearchActionKeyword;
    }
}