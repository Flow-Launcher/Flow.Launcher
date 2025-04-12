using Flow.Launcher.Plugin.Everything.Everything;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;

        public ObservableCollection<AccessLink> QuickAccessLinks { get; set; } = new();

        public ObservableCollection<AccessLink> IndexSearchExcludedSubdirectoryPaths { get; set; } = new ObservableCollection<AccessLink>();

        public string EditorPath { get; set; } = "";

        public string FolderEditorPath { get; set; } = "";

        public string ShellPath { get; set; } = "cmd";

        public string ExcludedFileTypes { get; set; } = "";


        public bool UseLocationAsWorkingDir { get; set; } = false;

        public bool ShowInlinedWindowsContextMenu { get; set; } = false;

        public string WindowsContextMenuIncludedItems { get; set; } = string.Empty;

        public string WindowsContextMenuExcludedItems { get; set; } = string.Empty;

        public bool DefaultOpenFolderInFileManager { get; set; } = false;

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

        public bool ShowFileSizeInPreviewPanel { get; set; } = true;

        public bool ShowCreatedDateInPreviewPanel { get; set; } = true;

        public bool ShowModifiedDateInPreviewPanel { get; set; } = true;

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

        [JsonIgnore]
        public SortOption[] SortOptions { get; set; } = Enum.GetValues<SortOption>();

        public SortOption SortOption { get; set; } = SortOption.NAME_ASCENDING;

        public bool EnableEverythingContentSearch { get; set; } = false;

        public bool EverythingEnabled => IndexSearchEngine == IndexSearchEngineOption.Everything ||
                                         PathEnumerationEngine == PathEnumerationEngineOption.Everything ||
                                         ContentSearchEngine == ContentIndexSearchEngineOption.Everything;

        public bool EverythingSearchFullPath { get; set; } = false;
        public bool EverythingEnableRunCount { get; set; } = true;

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
