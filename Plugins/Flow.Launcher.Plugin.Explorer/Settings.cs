using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;

        public ObservableCollection<AccessLink> QuickAccessLinks { get; set; } = [];

        public ObservableCollection<AccessLink> IndexSearchExcludedSubdirectoryPaths { get; set; } = [];

        public string EditorPath { get; set; } = "";

        public string FolderEditorPath { get; set; } = "";

        public string ShellPath { get; set; } = "cmd";

        public string ExcludedFileTypes { get; set; } = "";

        public bool UseLocationAsWorkingDir { get; set; } = false;

        public bool ShowInlinedWindowsContextMenu { get; set; } = false;

        public string WindowsContextMenuIncludedItems { get; set; } = string.Empty;

        public string WindowsContextMenuExcludedItems { get; set; } = string.Empty;

        public bool DefaultOpenFolderInFileManager { get; set; } = false;

        public bool DisplayMoreInformationInToolTip { get; set; } = false;

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


        public string FolderSearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool FolderSearchKeywordEnabled { get; set; }

        public string FileSearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool FileSearchKeywordEnabled { get; set; }

        public bool ExcludeQuickAccessFromActionKeywords { get; set; } = false;

        public bool WarnWindowsSearchServiceOff { get; set; } = true;

        public bool ShowFileSizeInPreviewPanel { get; set; } = true;

        public bool ShowCreatedDateInPreviewPanel { get; set; } = true;

        public bool ShowModifiedDateInPreviewPanel { get; set; } = true;
        
        public bool ShowFileAgeInPreviewPanel { get; set; } = false;

        public string PreviewPanelDateFormat { get; set; } = "yyyy-MM-dd";

        public string PreviewPanelTimeFormat { get; set; } = "HH:mm";

        private EverythingSearchManager _everythingManagerInstance;
        private WindowsIndexSearchManager _windowsIndexSearchManager;

        #region SearchEngine

        private EverythingSearchManager EverythingManagerInstance => _everythingManagerInstance ??= new EverythingSearchManager(this);
        private WindowsIndexSearchManager WindowsIndexSearchManager => _windowsIndexSearchManager ??= new WindowsIndexSearchManager(this);

        public IndexSearchEngineOption IndexSearchEngine { get; set; } = IndexSearchEngineOption.WindowsIndex;

        [JsonIgnore]
        public IIndexProvider IndexProvider => IndexSearchEngine switch
        {
            IndexSearchEngineOption.Everything => EverythingManagerInstance,
            IndexSearchEngineOption.WindowsIndex => WindowsIndexSearchManager,
            _ => throw new ArgumentOutOfRangeException(nameof(IndexSearchEngine))
        };

        public PathEnumerationEngineOption PathEnumerationEngine { get; set; } = PathEnumerationEngineOption.WindowsIndex;

        [JsonIgnore]
        public IPathIndexProvider PathEnumerator => PathEnumerationEngine switch
        {
            PathEnumerationEngineOption.Everything => EverythingManagerInstance,
            PathEnumerationEngineOption.WindowsIndex => WindowsIndexSearchManager,
            _ => throw new ArgumentOutOfRangeException(nameof(PathEnumerationEngine))
        };

        public ContentIndexSearchEngineOption ContentSearchEngine { get; set; } = ContentIndexSearchEngineOption.WindowsIndex;
        [JsonIgnore]
        public IContentIndexProvider ContentIndexProvider => ContentSearchEngine switch
        {
            ContentIndexSearchEngineOption.Everything => EverythingManagerInstance,
            ContentIndexSearchEngineOption.WindowsIndex => WindowsIndexSearchManager,
            _ => throw new ArgumentOutOfRangeException(nameof(ContentSearchEngine))
        };

        public enum PathEnumerationEngineOption
        {
            [Description("plugin_explorer_engine_windows_index")]
            WindowsIndex,
            [Description("plugin_explorer_engine_everything")]
            Everything,
            [Description("plugin_explorer_path_enumeration_engine_none")]
            DirectEnumeration
        }

        public enum IndexSearchEngineOption
        {
            [Description("plugin_explorer_engine_windows_index")]
            WindowsIndex,
            [Description("plugin_explorer_engine_everything")]
            Everything,
        }

        public enum ContentIndexSearchEngineOption
        {
            [Description("plugin_explorer_engine_windows_index")]
            WindowsIndex,
            [Description("plugin_explorer_engine_everything")]
            Everything,
        }

        #endregion

        #region Everything Settings

        public string EverythingInstalledPath { get; set; }

        public EverythingSortOption SortOption { get; set; } = EverythingSortOption.NAME_ASCENDING;

        public bool EnableEverythingContentSearch { get; set; } = false;

        public bool EverythingEnabled => IndexSearchEngine == IndexSearchEngineOption.Everything ||
                                         PathEnumerationEngine == PathEnumerationEngineOption.Everything ||
                                         ContentSearchEngine == ContentIndexSearchEngineOption.Everything;

        public bool EverythingSearchFullPath { get; set; } = false;
        public bool EverythingEnableRunCount { get; set; } = true;

        #endregion

        public enum ActionKeyword
        {
            SearchActionKeyword,
            PathSearchActionKeyword,
            FileContentSearchActionKeyword,
            IndexSearchActionKeyword,
            QuickAccessActionKeyword,
            FolderSearchActionKeyword,
            FileSearchActionKeyword,

        }

        internal string GetActionKeyword(ActionKeyword actionKeyword) => actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeyword,
            ActionKeyword.PathSearchActionKeyword => PathSearchActionKeyword,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchActionKeyword,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchActionKeyword,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessActionKeyword,
            ActionKeyword.FolderSearchActionKeyword => FolderSearchActionKeyword,
            ActionKeyword.FileSearchActionKeyword => FileSearchActionKeyword,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyWord property not found")
        };

        internal void SetActionKeyword(ActionKeyword actionKeyword, string keyword) => _ = actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeyword = keyword,
            ActionKeyword.PathSearchActionKeyword => PathSearchActionKeyword = keyword,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchActionKeyword = keyword,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchActionKeyword = keyword,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessActionKeyword = keyword,
            ActionKeyword.FolderSearchActionKeyword => FolderSearchActionKeyword = keyword,
            ActionKeyword.FileSearchActionKeyword => FileSearchActionKeyword = keyword,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyWord property not found")
        };

        internal bool GetActionKeywordEnabled(ActionKeyword actionKeyword) => actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeywordEnabled,
            ActionKeyword.PathSearchActionKeyword => PathSearchKeywordEnabled,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchKeywordEnabled,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchKeywordEnabled,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessKeywordEnabled,
            ActionKeyword.FolderSearchActionKeyword => FolderSearchKeywordEnabled,
            ActionKeyword.FileSearchActionKeyword => FileSearchKeywordEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyword enabled status not defined")
        };

        internal void SetActionKeywordEnabled(ActionKeyword actionKeyword, bool enable) => _ = actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeywordEnabled = enable,
            ActionKeyword.PathSearchActionKeyword => PathSearchKeywordEnabled = enable,
            ActionKeyword.IndexSearchActionKeyword => IndexSearchKeywordEnabled = enable,
            ActionKeyword.FileContentSearchActionKeyword => FileContentSearchKeywordEnabled = enable,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessKeywordEnabled = enable,
            ActionKeyword.FolderSearchActionKeyword => FolderSearchKeywordEnabled = enable,
            ActionKeyword.FileSearchActionKeyword => FileSearchKeywordEnabled = enable,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyword enabled status not defined")
        };

        public ActionKeyword? GetActiveActionKeyword(string actionKeywordStr)
        {
            if (string.IsNullOrEmpty(actionKeywordStr)) return null;
            foreach (ActionKeyword action in Enum.GetValues(typeof(ActionKeyword)))
            {
                var keywordStr = GetActionKeyword(action);
                if (string.IsNullOrEmpty(keywordStr)) continue;
                var isEnabled = GetActionKeywordEnabled(action);
                if (keywordStr == actionKeywordStr && isEnabled) return action;
            }
            return null;
        }

    }
}
