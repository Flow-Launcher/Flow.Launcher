using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public class SettingsViewModel
    {
        public Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public SettingsViewModel(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }


        public void Save()
        {
            Context.API.SaveSettingJsonStorage<Settings>();
        }


        public AccessLink SelectedQuickAccessLink { get; set; }
        public AccessLink SelectedIndexSearchExcludedPath { get; set; }
        public ActionKeywordModel SelectedActionKeyword { get; set; }



        public ICommand RemoveLinkCommand => new RelayCommand(RemoveLink);
        public ICommand EditLinkCommand => new RelayCommand(EditLink);
        public ICommand AddLinkCommand => new RelayCommand(AddLink);

        public ICommand EditActionKeywordCommand => new RelayCommand(EditActionKeyword);

        private void EditActionKeyword(object obj)
        {
            if (SelectedActionKeyword is not ActionKeywordModel actionKeyword)
            {
                ShowUnselectedMessage();
                return;
            }

            var actionKeywordWindow = new ActionKeywordSetting(actionKeyword, Context.API);

            if (actionKeywordWindow.ShowDialog() ?? false)
            {
                if (actionKeyword.Enabled && !actionKeywordWindow.KeywordEnabled)
                {
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                }
                else if (!actionKeyword.Enabled && actionKeywordWindow.KeywordEnabled)
                {
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                }
                else if (actionKeyword.Enabled && actionKeywordWindow.KeywordEnabled)
                {
                    // same keyword will have dialog result false
                    Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, actionKeyword.Keyword);
                    Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, actionKeywordWindow.ActionKeyword);
                }

                (actionKeyword.Keyword, actionKeyword.Enabled) = (actionKeywordWindow.ActionKeyword, actionKeywordWindow.KeywordEnabled);
            }

        }

        private AccessLink? PromptUserSelectPath(ResultType type, string initialDirectory = null)
        {
            AccessLink newAccessLink = null;

            if (type is ResultType.Folder)
            {
                var folderBrowserDialog = new FolderBrowserDialog();

                if (initialDirectory is not null)
                    folderBrowserDialog.InitialDirectory = initialDirectory;

                if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                    return newAccessLink;

                newAccessLink = new AccessLink { Path = folderBrowserDialog.SelectedPath };
            }
            else if (type is ResultType.File)
            {
                var openFileDialog = new OpenFileDialog();
                if (initialDirectory is not null)
                    openFileDialog.InitialDirectory = initialDirectory;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return newAccessLink;

                newAccessLink = new AccessLink { Path = openFileDialog.FileName };
            }
            return newAccessLink;
        }

        private void EditLink(object obj)
        {
            if (obj is not string container) return;

            AccessLink selectedLink;
            ObservableCollection<AccessLink> collection;

            switch (container)
            {
                case "QuickAccessLink":
                    if (SelectedQuickAccessLink == null)
                    {
                        ShowUnselectedMessage();
                        return;
                    }
                    selectedLink = SelectedQuickAccessLink;
                    collection = Settings.QuickAccessLinks;
                    break;
                case "IndexSearchExcludedPaths":
                    if (SelectedIndexSearchExcludedPath == null)
                    {
                        ShowUnselectedMessage();
                        return;
                    }
                    selectedLink = SelectedIndexSearchExcludedPath;
                    collection = Settings.IndexSearchExcludedSubdirectoryPaths;
                    break;
                default:
                    return;
            }

            var link = PromptUserSelectPath(selectedLink.Type,
                selectedLink.Type == ResultType.Folder
                    ? selectedLink.Path
                    : Path.GetDirectoryName(selectedLink.Path));

            if (link is null)
                return;

            collection.Remove(selectedLink);
            collection.Add(link);
        }

        private void ShowUnselectedMessage()
        {
            string warning = Context.API.GetTranslation("plugin_explorer_make_selection_warning");
            MessageBox.Show(warning);
        }

        private void AddLink(object obj)
        {
            if (obj is not string container) return;

            var folderBrowserDialog = new FolderBrowserDialog();

            if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                return;

            var newAccessLink = new AccessLink { Path = folderBrowserDialog.SelectedPath };


            switch (container)
            {
                case "QuickAccessLink":
                    if (SelectedQuickAccessLink == null) return;
                    Settings.QuickAccessLinks.Add(newAccessLink);
                    break;
                case "IndexSearchExcludedPaths":
                    if (SelectedIndexSearchExcludedPath == null) return;
                    Settings.IndexSearchExcludedSubdirectoryPaths.Add(newAccessLink);
                    break;
            }
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



        internal void RemoveLinkFromQuickAccess(AccessLink selectedRow) => Settings.QuickAccessLinks.Remove(selectedRow);

        internal void RemoveAccessLinkFromExcludedIndexPaths(AccessLink selectedRow) => Settings.IndexSearchExcludedSubdirectoryPaths.Remove(selectedRow);

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

        internal void UpdateActionKeyword(Settings.ActionKeyword modifiedActionKeyword, string newActionKeyword, string oldActionKeyword) =>
            PluginManager.ReplaceActionKeyword(Context.CurrentPluginMetadata.ID, oldActionKeyword, newActionKeyword);

        internal bool IsActionKeywordAlreadyAssigned(string newActionKeyword) => PluginManager.ActionKeywordRegistered(newActionKeyword);

        internal bool IsNewActionKeywordGlobal(string newActionKeyword) => newActionKeyword == Query.GlobalPluginWildcardSign;

        public bool UseWindowsIndexForDirectorySearch {
            get
            {
                return Settings.UseWindowsIndexForDirectorySearch;
            }
            set
            {
                Settings.UseWindowsIndexForDirectorySearch = value;
            }
        }
    }
}