using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.FolderLinks;
using System.Diagnostics;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public class SettingsViewModel
    {
        private readonly PluginJsonStorage<Settings> storage;

        internal Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public SettingsViewModel(PluginInitContext context)
        {
            Context = context;
            storage = new PluginJsonStorage<Settings>();
            Settings = storage.Load();
        }

        public void Save()
        {
            storage.Save();
        }

        internal void RemoveFolderLinkFromQuickFolders(FolderLink selectedRow) => Settings.QuickFolderAccessLinks.Remove(selectedRow);

        internal void RemoveFolderLinkFromExcludedIndexPaths(FolderLink selectedRow) => Settings.IndexSearchExcludedSubdirectoryPaths.Remove(selectedRow);

        internal void OpenWindowsIndexingOptions()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "control.exe",
                UseShellExecute = true,
                Arguments = Constants.WindowsIndexingOptions
            };

            Process.Start(psi);
        }

        internal void UpdateActionKeyword(string newActionKeyword, string oldActionKeyword)
        {
            PluginManager.ReplaceActionKeyword(Context.CurrentPluginMetadata.ID, oldActionKeyword, newActionKeyword);

            if (Settings.FileContentSearchActionKeyword == oldActionKeyword)
                Settings.FileContentSearchActionKeyword = newActionKeyword;

            if (Settings.SearchActionKeyword == oldActionKeyword)
                Settings.SearchActionKeyword = newActionKeyword;
        }

        internal bool IsNewActionKeywordGlobal(string newActionKeyword) => newActionKeyword == Query.GlobalPluginWildcardSign;
    }
}
