using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLink;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;

        public List<AccessLink> QuickFolderAccessLinks { get; set; } = new List<AccessLink>();

        public bool UseWindowsIndexForDirectorySearch { get; set; } = true;

        public List<AccessLink> IndexSearchExcludedSubdirectoryPaths { get; set; } = new List<AccessLink>();

        public string SearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public string FileContentSearchActionKeyword { get; set; } = Constants.DefaultContentSearchActionKeyword;
    }
}