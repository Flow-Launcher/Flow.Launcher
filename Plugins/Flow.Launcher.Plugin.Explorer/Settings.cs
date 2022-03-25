using Flow.Launcher.Plugin.Everything.Everything;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public bool SearchActionKeywordEnabled { get; set; } = true;

        public string FileContentSearchActionKeyword { get; set; } = Constants.DefaultContentSearchActionKeyword;

        public bool FileContentSearchKeywordEnabled { get; set; } = true;

        public string PathSearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool PathSearchKeywordEnabled { get; set; }

        public string IndexSearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool IndexSearchKeywordEnabled { get; set; }

        public string QuickAccessActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool QuickAccessKeywordEnabled { get; set; }

        public bool WarnWindowsSearchServiceOff { get; set; } = true;

        private List<IIndexProvider> _indexProviders;
        public Settings()
        {
            _indexProviders = new List<IIndexProvider>()
            {
                new EverythingSearchManager(this),
                new WindowsIndexManager(this)
            };
        }
        public IndexSearchEngineOption IndexSearchEngine { get; set; }
        public IIndexProvider IndexProvider => _indexProviders[(int)IndexSearchEngine];

        public enum PathTraversalEngineOption
        {
            Everything,
            WindowsIndex,
            Direct
        }

        public enum IndexSearchEngineOption
        {
            Everything,
            WindowsIndex
        }
        #region Everything Settings

        
        public bool LaunchHidden { get; set; } = false;

        public string EverythingInstalledPath { get; set; }

        public SortOption[] SortOptions { get; set; } = Enum.GetValues<SortOption>();

        public SortOption SortOption { get; set; } = SortOption.NAME_ASCENDING;

        #endregion

        internal enum ActionKeyword
        {
            SearchActionKeyword,
            PathSearchActionKeyword,
            FileContentSearchActionKeyword,
            IndexSearchActionKeyword,
            QuickAccessActionKeyword
        }

        internal string GetActionKeyword(ActionKeyword actionKeyword) => actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeyword,
            ActionKeyword.PathSearchActionKeyword => PathSearchActionKeyword,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchActionKeyword,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchActionKeyword,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessActionKeyword,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyWord property not found")
        };

        internal void SetActionKeyword(ActionKeyword actionKeyword, string keyword) => _ = actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeyword = keyword,
            ActionKeyword.PathSearchActionKeyword => PathSearchActionKeyword = keyword,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchActionKeyword = keyword,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchActionKeyword = keyword,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessActionKeyword = keyword,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyWord property not found")
        };

        internal bool GetActionKeywordEnabled(ActionKeyword actionKeyword) => actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeywordEnabled,
            ActionKeyword.PathSearchActionKeyword => PathSearchKeywordEnabled,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchKeywordEnabled,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchKeywordEnabled,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessKeywordEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyword enabled status not defined")
        };

        internal void SetActionKeywordEnabled(ActionKeyword actionKeyword, bool enable) => _ = actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeywordEnabled = enable,
            ActionKeyword.PathSearchActionKeyword => PathSearchKeywordEnabled = enable,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchKeywordEnabled = enable,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchKeywordEnabled = enable,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessKeywordEnabled = enable,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyword enabled status not defined")
        };
    }
}