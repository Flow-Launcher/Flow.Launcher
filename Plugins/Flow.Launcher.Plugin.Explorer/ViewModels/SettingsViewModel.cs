#nullable enable
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        public Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public IReadOnlyList<EnumBindingModel<Settings.IndexSearchEngineOption>> IndexSearchEngines { get; set; }
        public IReadOnlyList<EnumBindingModel<Settings.ContentIndexSearchEngineOption>> ContentIndexSearchEngines { get; set; }
        public IReadOnlyList<EnumBindingModel<Settings.PathEnumerationEngineOption>> PathEnumerationEngines { get; set; }

        public SettingsViewModel(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;

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
                OnPropertyChanged(nameof(PreviewPanelDateTimeChoicesVisibility));
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
                OnPropertyChanged(nameof(PreviewPanelDateTimeChoicesVisibility));
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

        public Visibility PreviewPanelDateTimeChoicesVisibility => ShowCreatedDateInPreviewPanel || ShowModifiedDateInPreviewPanel ? Visibility.Visible : Visibility.Collapsed;


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

        public ICommand EditActionKeywordCommand => new RelayCommand(EditActionKeyword);

        private void EditActionKeyword(object obj)
        {
            if (SelectedActionKeyword is not { } actionKeyword)
            {
                ShowUnselectedMessage();
                return;
            }

            var actionKeywordWindow = new ActionKeywordSetting(actionKeyword, Context.API);

            if (!(actionKeywordWindow.ShowDialog() ?? false))
            {
                return;
            }

            switch (actionKeyword.Enabled, actionKeywordWindow.KeywordEnabled)
            {
                case (true, false):
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    break;
                case (true, true):
                    // same keyword will have dialog result false
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, actionKeywordWindow.ActionKeyword);
                    break;
                case (false, true):
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, actionKeywordWindow.ActionKeyword);
                    break;
                case (false, false):
                    throw new ArgumentException(
                        $"Both false in {nameof(actionKeyword)}.{nameof(actionKeyword.Enabled)} and {nameof(actionKeywordWindow)}.{nameof(actionKeywordWindow.KeywordEnabled)} should suggest that the ShowDialog() result is false");
            }

            (actionKeyword.Keyword, actionKeyword.Enabled) = (actionKeywordWindow.ActionKeyword, actionKeywordWindow.KeywordEnabled);

        }

        #endregion

        #region AccessLinks

        public AccessLink? SelectedQuickAccessLink { get; set; }
        public AccessLink? SelectedIndexSearchExcludedPath { get; set; }



        public ICommand RemoveLinkCommand => new RelayCommand(RemoveLink);
        public ICommand EditLinkCommand => new RelayCommand(EditLink);
        public ICommand AddLinkCommand => new RelayCommand(AddLink);

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

        private void EditLink(object commandParameter)
        {
            var (selectedLink, collection) = commandParameter switch
            {
                "QuickAccessLink" => (SelectedQuickAccessLink, Settings.QuickAccessLinks),
                "IndexSearchExcludedPaths" => (SelectedIndexSearchExcludedPath, Settings.IndexSearchExcludedSubdirectoryPaths),
                _ => throw new ArgumentOutOfRangeException(nameof(commandParameter))
            };

            if (selectedLink is null)
            {
                ShowUnselectedMessage();
                return;
            }

            var path = PromptUserSelectPath(selectedLink.Type,
                selectedLink.Type == ResultType.Folder
                    ? selectedLink.Path
                    : Path.GetDirectoryName(selectedLink.Path));

            if (path is null)
                return;

            collection.Remove(selectedLink);
            collection.Add(new AccessLink
            {
                Path = path, Type = selectedLink.Type,
            });
        }

        private void ShowUnselectedMessage()
        {
            var warning = Context.API.GetTranslation("plugin_explorer_make_selection_warning");
            MessageBox.Show(warning);
        }


        private void AddLink(object commandParameter)
        {
            var container = commandParameter switch
            {
                "QuickAccessLink" => Settings.QuickAccessLinks,
                "IndexSearchExcludedPaths" => Settings.IndexSearchExcludedSubdirectoryPaths,
                _ => throw new ArgumentOutOfRangeException(nameof(commandParameter))
            };

            ArgumentNullException.ThrowIfNull(container);

            var folderBrowserDialog = new FolderBrowserDialog();

            if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                return;

            var newAccessLink = new AccessLink
            {
                Path = folderBrowserDialog.SelectedPath
            };

            container.Add(newAccessLink);
        }

        private void RemoveLink(object obj)
        {
            if (obj is not string container) return;

            switch (container)
            {
                case "QuickAccessLink":
                    if (SelectedQuickAccessLink == null) return;
                    Settings.QuickAccessLinks.Remove(SelectedQuickAccessLink);
                    break;
                case "IndexSearchExcludedPaths":
                    if (SelectedIndexSearchExcludedPath == null) return;
                    Settings.IndexSearchExcludedSubdirectoryPaths.Remove(SelectedIndexSearchExcludedPath);
                    break;
            }
            Save();
        }

        #endregion

        private string? PromptUserSelectPath(ResultType type, string? initialDirectory = null)
        {
            string? path = null;

            if (type is ResultType.Folder)
            {
                var folderBrowserDialog = new FolderBrowserDialog();

                if (initialDirectory is not null)
                    folderBrowserDialog.InitialDirectory = initialDirectory;

                if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                    return path;

                path = folderBrowserDialog.SelectedPath;
            }
            else if (type is ResultType.File)
            {
                var openFileDialog = new OpenFileDialog();
                if (initialDirectory is not null)
                    openFileDialog.InitialDirectory = initialDirectory;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return path;

                path = openFileDialog.FileName;
            }
            return path;
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

        private ICommand? _openFileEditorPathCommand;

        public ICommand OpenFileEditorPath => _openFileEditorPathCommand ??= new RelayCommand(_ =>
        {
            var path = PromptUserSelectPath(ResultType.File, Settings.EditorPath != null ? Path.GetDirectoryName(Settings.EditorPath) : null);
            if (path is null)
                return;

            FileEditorPath = path;
        });

        private ICommand? _openFolderEditorPathCommand;

        public ICommand OpenFolderEditorPath => _openFolderEditorPathCommand ??= new RelayCommand(_ =>
        {
            var path = PromptUserSelectPath(ResultType.File, Settings.FolderEditorPath != null ? Path.GetDirectoryName(Settings.FolderEditorPath) : null);
            if (path is null)
                return;

            FolderEditorPath = path;
        });

        private ICommand? _openShellPathCommand;

        public ICommand OpenShellPath => _openShellPathCommand ??= new RelayCommand(_ =>
        {
            var path = PromptUserSelectPath(ResultType.File, Settings.EditorPath != null ? Path.GetDirectoryName(Settings.EditorPath) : null);
            if (path is null)
                return;

            ShellPath = path;
        });


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


        #region Everything FastSortWarning

        public Visibility FastSortWarningVisibility
        {
            get
            {
                try
                {
                    return EverythingApi.IsFastSortOption(Settings.SortOption) ? Visibility.Collapsed : Visibility.Visible;
                }
                catch (IPCErrorException)
                {
                    // this error occurs if the Everything service is not running, in this instance show the warning and
                    // update the message to let user know in the settings panel.
                    return Visibility.Visible;
                }
                catch (DllNotFoundException)
                {
                    return Visibility.Collapsed;
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
                        : Context.API.GetTranslation("flowlauncher_plugin_everything_nonfastsort_warning");
                }
                catch (IPCErrorException)
                {
                    return Context.API.GetTranslation("flowlauncher_plugin_everything_is_not_running");
                }
                catch (DllNotFoundException)
                {
                    return Context.API.GetTranslation("flowlauncher_plugin_everything_sdk_issue");
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
