using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;

        public List<AccessLink> QuickAccessLinks { get; set; } = new List<AccessLink>();

        // as at v1.7.0 this is to maintain backwards compatibility, need to be removed afterwards.
        public List<AccessLink> QuickFolderAccessLinks { get; set; } = new List<AccessLink>();

        public bool UseWindowsIndexForDirectorySearch { get; set; } = true;

        public List<AccessLink> IndexSearchExcludedSubdirectoryPaths { get; set; } = new List<AccessLink>();

        public string SearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public string FileContentSearchActionKeyword { get; set; } = Constants.DefaultContentSearchActionKeyword;

        public string PathSearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public string IndexOnlySearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;
    }
}