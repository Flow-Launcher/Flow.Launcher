#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Views.Avalonia;
using AvaloniaApp = Avalonia.Application;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public partial class SettingsViewModel : BaseModel
    {
        /// <summary>
        /// Gets the current active Avalonia window to use as dialog owner
        /// </summary>
        private static global::Avalonia.Controls.Window? GetAvaloniaOwnerWindow()
        {
            if (AvaloniaApp.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            // First try to find an active window
            var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsActive);
            if (activeWindow != null)
                return activeWindow;

            // Fall back to main window
            return desktop.MainWindow;
        }

        public Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public IReadOnlyList<EnumBindingModel<Settings.IndexSearchEngineOption>> IndexSearchEngines { get; set; }
        public IReadOnlyList<EnumBindingModel<Settings.ContentIndexSearchEngineOption>> ContentIndexSearchEngines { get; set; }
        public IReadOnlyList<EnumBindingModel<Settings.PathEnumerationEngineOption>> PathEnumerationEngines { get; set; }

        public SettingsViewModel(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;

            ActionKeywordModel.Init(settings);
            InitializeEngineSelection();
            InitializeActionKeywordModels();
        }

        public void Save()
        {
            Context.API.SaveSettingJsonStorage<Settings>();
        }

        #region Engine Selection

        private EnumBindingModel<Settings.IndexSearchEngineOption> _selectedIndexSearchEngine;
        private EnumBindingModel<Settings.ContentIndexSearchEngineOption> _selectedContentSearchEngine;
        private EnumBindingModel<Settings.PathEnumerationEngineOption> _selectedPathEnumerationEngine;

        public EnumBindingModel<Settings.IndexSearchEngineOption> SelectedIndexSearchEngine
        {
            get => _selectedIndexSearchEngine;
            set
            {
                _selectedIndexSearchEngine = value;
                Settings.IndexSearchEngine = value.Value;
                OnPropertyChanged();
            }
        }

        public EnumBindingModel<Settings.ContentIndexSearchEngineOption> SelectedContentSearchEngine
        {
            get => _selectedContentSearchEngine;
            set
            {
                _selectedContentSearchEngine = value;
                Settings.ContentSearchEngine = value.Value;
                OnPropertyChanged();
            }
        }

        public EnumBindingModel<Settings.PathEnumerationEngineOption> SelectedPathEnumerationEngine
        {
            get => _selectedPathEnumerationEngine;
            set
            {
                _selectedPathEnumerationEngine = value;
                Settings.PathEnumerationEngine = value.Value;
                OnPropertyChanged();
            }
        }

        [MemberNotNull(nameof(IndexSearchEngines),
            nameof(ContentIndexSearchEngines),
            nameof(PathEnumerationEngines),
            nameof(_selectedIndexSearchEngine),
            nameof(_selectedContentSearchEngine),
            nameof(_selectedPathEnumerationEngine))]
        private void InitializeEngineSelection()
        {
            IndexSearchEngines = EnumBindingModel<Settings.IndexSearchEngineOption>.CreateList();
            ContentIndexSearchEngines = EnumBindingModel<Settings.ContentIndexSearchEngineOption>.CreateList();
            PathEnumerationEngines = EnumBindingModel<Settings.PathEnumerationEngineOption>.CreateList();

            _selectedIndexSearchEngine = IndexSearchEngines.First(x => x.Value == Settings.IndexSearchEngine);
            _selectedContentSearchEngine = ContentIndexSearchEngines.First(x => x.Value == Settings.ContentSearchEngine);
            _selectedPathEnumerationEngine = PathEnumerationEngines.First(x => x.Value == Settings.PathEnumerationEngine);
        }

        #endregion

        #region Native Context Menu

        public bool ShowWindowsContextMenu
        {
            get => Settings.ShowInlinedWindowsContextMenu;
            set
            {
                Settings.ShowInlinedWindowsContextMenu = value;
                OnPropertyChanged();
            }
        }

        public string WindowsContextMenuIncludedItems
        {
            get => Settings.WindowsContextMenuIncludedItems;
            set
            {
                Settings.WindowsContextMenuIncludedItems = value;
                OnPropertyChanged();
            }
        }

        public string WindowsContextMenuExcludedItems
        {
            get => Settings.WindowsContextMenuExcludedItems;
            set
            {
                Settings.WindowsContextMenuExcludedItems = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Preview Panel

        public bool ShowFileSizeInPreviewPanel
        {
            get => Settings.ShowFileSizeInPreviewPanel;
            set
            {
                Settings.ShowFileSizeInPreviewPanel = value;
                OnPropertyChanged();
            }
        }

        public bool ShowCreatedDateInPreviewPanel
        {
            get => Settings.ShowCreatedDateInPreviewPanel;
            set
            {
                Settings.ShowCreatedDateInPreviewPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
            }
        }

        public bool ShowModifiedDateInPreviewPanel
        {
            get => Settings.ShowModifiedDateInPreviewPanel;
            set
            {
                Settings.ShowModifiedDateInPreviewPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
            }
        }

        public bool ShowFileAgeInPreviewPanel
        {
            get => Settings.ShowFileAgeInPreviewPanel;
            set
            {
                Settings.ShowFileAgeInPreviewPanel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPreviewPanelDateTimeChoices));
            }
        }

        public string PreviewPanelDateFormat
        {
            get => Settings.PreviewPanelDateFormat;
            set
            {
                Settings.PreviewPanelDateFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewPanelDateFormatDemo));
            }
        }

        public string PreviewPanelTimeFormat
        {
            get => Settings.PreviewPanelTimeFormat;
            set
            {
                Settings.PreviewPanelTimeFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewPanelTimeFormatDemo));
            }
        }

        public string PreviewPanelDateFormatDemo => DateTime.Now.ToString(PreviewPanelDateFormat, CultureInfo.CurrentCulture);
        public string PreviewPanelTimeFormatDemo => DateTime.Now.ToString(PreviewPanelTimeFormat, CultureInfo.CurrentCulture);

        public bool ShowPreviewPanelDateTimeChoices => ShowCreatedDateInPreviewPanel || ShowModifiedDateInPreviewPanel;

        public List<string> TimeFormatList { get; } = new()
        {
            "h:mm",
            "hh:mm",
            "H:mm",
            "HH:mm",
            "tt h:mm",
            "tt hh:mm",
            "h:mm tt",
            "hh:mm tt",
            "hh:mm:ss tt",
            "HH:mm:ss"
        };


        public List<string> DateFormatList { get; } = new()
        {
            "dd/MM/yyyy",
            "dd/MM/yyyy ddd",
            "dd/MM/yyyy, dddd",
            "dd-MM-yyyy",
            "dd-MM-yyyy ddd",
            "dd-MM-yyyy, dddd",
            "dd.MM.yyyy",
            "dd.MM.yyyy ddd",
            "dd.MM.yyyy, dddd",
            "MM/dd/yyyy",
            "MM/dd/yyyy ddd",
            "MM/dd/yyyy, dddd",
            "yyyy-MM-dd",
            "yyyy-MM-dd ddd",
            "yyyy-MM-dd, dddd",
            "dd/MMM/yyyy",
            "dd/MMM/yyyy ddd",
            "dd/MMM/yyyy, dddd",
            "dd-MMM-yyyy",
            "dd-MMM-yyyy ddd",
            "dd-MMM-yyyy, dddd",
            "dd.MMM.yyyy",
            "dd.MMM.yyyy ddd",
            "dd.MMM.yyyy, dddd",
            "MMM/dd/yyyy",
            "MMM/dd/yyyy ddd",
            "MMM/dd/yyyy, dddd",
            "yyyy-MMM-dd",
            "yyyy-MMM-dd ddd",
            "yyyy-MMM-dd, dddd",
        };

        #endregion

        #region ActionKeyword

        [MemberNotNull(nameof(ActionKeywordsModels))]
        private void InitializeActionKeywordModels()
        {
            ActionKeywordsModels = new List<ActionKeywordModel>
            {
                new(Settings.ActionKeyword.SearchActionKeyword,
                    "plugin_explorer_actionkeywordview_search"),
                new(Settings.ActionKeyword.FileContentSearchActionKeyword,
                    "plugin_explorer_actionkeywordview_filecontentsearch"),
                new(Settings.ActionKeyword.PathSearchActionKeyword,
                    "plugin_explorer_actionkeywordview_pathsearch"),
                new(Settings.ActionKeyword.IndexSearchActionKeyword,
                    "plugin_explorer_actionkeywordview_indexsearch"),
                new(Settings.ActionKeyword.QuickAccessActionKeyword,
                    "plugin_explorer_actionkeywordview_quickaccess")
            };
        }

        public IReadOnlyList<ActionKeywordModel> ActionKeywordsModels { get; set; }

        public ActionKeywordModel? SelectedActionKeyword { get; set; }

        [RelayCommand]
        private async Task EditActionKeywordAsync(object obj)
        {
            if (SelectedActionKeyword is not { } actionKeyword)
            {
                ShowUnselectedMessage();
                return;
            }

            var dialog = new ActionKeywordSetting(actionKeyword);
            var ownerWindow = GetAvaloniaOwnerWindow();
            bool dialogResult;
            if (ownerWindow != null)
            {
                dialogResult = await dialog.ShowDialog<bool?>(ownerWindow) ?? false;
            }
            else
            {
                // Fallback: show as normal window if owner is not available
                dialog.Show();
                var tcs = new TaskCompletionSource<bool?>();
                dialog.Closed += (_, _) => tcs.TrySetResult(true);
                await tcs.Task;
                dialogResult = true;
            }

            if (!dialogResult)
            {
                return;
            }

            var newKeyword = dialog.ActionKeyword;
            var newEnabled = dialog.KeywordEnabled;

            switch (actionKeyword.Enabled, newEnabled)
            {
                case (true, false):
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    break;
                case (true, true):
                    // same keyword will have dialog result false
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, newKeyword);
                    break;
                case (false, true):
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, newKeyword);
                    break;
                case (false, false):
                    break;
            }

            (actionKeyword.Keyword, actionKeyword.Enabled) = (newKeyword, newEnabled);
        }

        #endregion

        #region AccessLinks

        public AccessLink? SelectedQuickAccessLink { get; set; }
        public AccessLink? SelectedIndexSearchExcludedPath { get; set; }

        public void AppendLink(string containerName, AccessLink link)
        {
            var container = containerName switch
            {
                "QuickAccessLink" => Settings.QuickAccessLinks,
                "IndexSearchExcludedPaths" => Settings.IndexSearchExcludedSubdirectoryPaths,
                _ => throw new ArgumentException($"Unknown container name: {containerName}")
            };
            container.Add(link);
        }

        [RelayCommand]
        private async Task EditIndexSearchExcludePathsAsync()
        {
            var selectedLink = SelectedIndexSearchExcludedPath;
            var collection = Settings.IndexSearchExcludedSubdirectoryPaths;

            if (selectedLink is null)
            {
                ShowUnselectedMessage();
                return;
            }

            var path = await PromptUserSelectPathAsync(selectedLink.Type,
                selectedLink.Type == ResultType.Folder
                    ? selectedLink.Path
                    : Path.GetDirectoryName(selectedLink.Path));

            if (path is null)
                return;

            collection.Remove(selectedLink);
            collection.Add(new AccessLink
            {
                Path = path, Type = selectedLink.Type, Name = path.GetPathName()
            });
            Save();
        }

        [RelayCommand]
        private async Task AddIndexSearchExcludePathsAsync()
        {
            var container = Settings.IndexSearchExcludedSubdirectoryPaths;

            if (container is null) return;

            var path = await PromptUserSelectFolderAsync();

            if (path is null)
                return;

            var newAccessLink = new AccessLink
            {
                Name = path.GetPathName(),
                Path = path
            };

            container.Add(newAccessLink);
            Save();
        }

        [RelayCommand]
        private async Task EditQuickAccessLinkAsync()
        {
            var selectedLink = SelectedQuickAccessLink;
            var collection = Settings.QuickAccessLinks;

            if (selectedLink is null)
            {
                ShowUnselectedMessage();
                return;
            }

            var dialog = new QuickAccessLinkSettings(collection, selectedLink);
            var ownerWindow = GetAvaloniaOwnerWindow();
            bool dialogResult;
            if (ownerWindow != null)
            {
                dialogResult = await dialog.ShowDialog<bool?>(ownerWindow) ?? false;
            }
            else
            {
                // Fallback: show as normal window if owner is not available
                dialog.Show();
                var tcs = new TaskCompletionSource<bool?>();
                dialog.Closed += (_, _) => tcs.TrySetResult(true);
                await tcs.Task;
                dialogResult = true;
            }

            if (dialogResult)
            {
                Save();
            }
        }

        [RelayCommand]
        private async Task AddQuickAccessLinkAsync()
        {
            var dialog = new QuickAccessLinkSettings(Settings.QuickAccessLinks);
            var ownerWindow = GetAvaloniaOwnerWindow();
            bool dialogResult;
            if (ownerWindow != null)
            {
                dialogResult = await dialog.ShowDialog<bool?>(ownerWindow) ?? false;
            }
            else
            {
                // Fallback: show as normal window if owner is not available
                dialog.Show();
                var tcs = new TaskCompletionSource<bool?>();
                dialog.Closed += (_, _) => tcs.TrySetResult(true);
                await tcs.Task;
                dialogResult = true;
            }

            if (dialogResult)
            {
                Save();
            }
        }

        [RelayCommand]
        private void RemoveLink(object commandParameter)
        {
            if (commandParameter is not string container) return;

            switch (container)
            {
                case "QuickAccessLink":
                    if (SelectedQuickAccessLink == null) return;
                    if (Context.API.ShowMsgBox(
                            Localize.plugin_explorer_delete_quick_access_link(),
                            Localize.plugin_explorer_delete(),
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Warning)
                        == MessageBoxResult.Cancel)
                        return;
                    Settings.QuickAccessLinks.Remove(SelectedQuickAccessLink);
                    break;
                case "IndexSearchExcludedPaths":
                    if (SelectedIndexSearchExcludedPath == null) return;
                    if (Context.API.ShowMsgBox(
                            Localize.plugin_explorer_delete_index_search_excluded_path(),
                            Localize.plugin_explorer_delete(),
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Warning)
                        == MessageBoxResult.Cancel)
                        return;
                    Settings.IndexSearchExcludedSubdirectoryPaths.Remove(SelectedIndexSearchExcludedPath);
                    break;
            }
            Save();
        }

        private void ShowUnselectedMessage()
        {
            var warning = Localize.plugin_explorer_make_selection_warning();
            Context.API.ShowMsgBox(warning);
        }

        private static async Task<string?> PromptUserSelectPathAsync(ResultType type, string? initialDirectory = null)
        {
            string? path = null;

            if (type is ResultType.Folder)
            {
                path = await PromptUserSelectFolderAsync(initialDirectory);
            }
            else if (type is ResultType.File)
            {
                path = await PromptUserSelectFileAsync(initialDirectory);
            }
            return path;
        }

        private static async Task<string?> PromptUserSelectFolderAsync(string? initialDirectory = null)
        {
            var mainWindow = AvaloniaApp.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null) return null;

            var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        }

        private static async Task<string?> PromptUserSelectFileAsync(string? initialDirectory = null)
        {
            var mainWindow = AvaloniaApp.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null) return null;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                AllowMultiple = false
            });

            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }

        internal static void OpenWindowsIndexingOptions()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "control.exe",
                UseShellExecute = true,
                Arguments = Constants.WindowsIndexingOptions
            };

            Process.Start(psi);
        }

        [RelayCommand]
        private async Task OpenFileEditorPathAsync()
        {
            var path = await PromptUserSelectFileAsync(Settings.EditorPath != null ? Path.GetDirectoryName(Settings.EditorPath) : null);
            if (path is null)
                return;

            FileEditorPath = path;
        }

        [RelayCommand]
        private async Task OpenFolderEditorPathAsync()
        {
            var path = await PromptUserSelectFolderAsync(Settings.FolderEditorPath != null ? Path.GetDirectoryName(Settings.FolderEditorPath) : null);
            if (path is null)
                return;

            FolderEditorPath = path;
        }

        [RelayCommand]
        private async Task OpenShellPathAsync()
        {
            var path = await PromptUserSelectFileAsync(Settings.EditorPath != null ? Path.GetDirectoryName(Settings.EditorPath) : null);
            if (path is null)
                return;

            ShellPath = path;
        }

        public string FileEditorPath
        {
            get => Settings.EditorPath;
            set
            {
                Settings.EditorPath = value;
                OnPropertyChanged();
            }
        }

        public string FolderEditorPath
        {
            get => Settings.FolderEditorPath;
            set
            {
                Settings.FolderEditorPath = value;
                OnPropertyChanged();
            }
        }

        public string ShellPath
        {
            get => Settings.ShellPath;
            set
            {
                Settings.ShellPath = value;
                OnPropertyChanged();
            }
        }

        public string ExcludedFileTypes
        {
            get => Settings.ExcludedFileTypes;
            set
            {
                // remove spaces and dots from the string before saving
                string sanitized = string.IsNullOrEmpty(value) ? "" : value.Replace(" ", "").Replace(".", "");
                Settings.ExcludedFileTypes = sanitized;
                OnPropertyChanged();
            }
        }

        public int MaxResultLowerLimit { get; } = 1;
        public int MaxResultUpperLimit { get; } = 100000;

        public int MaxResult
        {
            get => Settings.MaxResult;
            set
            {
                Settings.MaxResult = Math.Clamp(value, MaxResultLowerLimit, MaxResultUpperLimit);
                OnPropertyChanged();
            }
        }

        #endregion

        #region Everything FastSortWarning

        public List<EverythingSortOptionLocalized> AllEverythingSortOptions { get; } = EverythingSortOptionLocalized.GetValues();

        public EverythingSortOption SelectedEverythingSortOption
        {
            get => Settings.SortOption;
            set
            {
                if (value == Settings.SortOption)
                    return;
                Settings.SortOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FastSortWarningVisibility));
                OnPropertyChanged(nameof(SortOptionWarningMessage));
            }
        }

        public bool FastSortWarningVisibility
        {
            get
            {
                try
                {
                    return !EverythingApi.IsFastSortOption(Settings.SortOption);
                }
                catch (IPCErrorException)
                {
                    // this error occurs if the Everything service is not running, in this instance show the warning and
                    // update the message to let user know in the settings panel.
                    return true;
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
            }
        }

        public string SortOptionWarningMessage
        {
            get
            {
                try
                {
                    // this method is used to determine if Everything service is running because as at Everything v1.4.1
                    // the sdk does not provide a dedicated interface to determine if it is running.
                    return EverythingApi.IsFastSortOption(Settings.SortOption) ? string.Empty
                        : Localize.flowlauncher_plugin_everything_nonfastsort_warning();
                }
                catch (IPCErrorException)
                {
                    return Localize.flowlauncher_plugin_everything_is_not_running();
                }
                catch (DllNotFoundException)
                {
                    return Localize.flowlauncher_plugin_everything_sdk_issue();
                }
            }
        }

        public string EverythingInstalledPath
        {
            get => Settings.EverythingInstalledPath;
            set
            {
                Settings.EverythingInstalledPath = value;
                OnPropertyChanged();
            }
        }

        #endregion
    }
}
