using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using System.Diagnostics;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public class SettingsViewModel
    {
        internal Settings Settings { get; set; }

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

        internal void RemoveLinkFromQuickAccess(AccessLink selectedRow) => Settings.QuickAccessLinks.Remove(selectedRow);

        internal void RemoveAccessLinkFromExcludedIndexPaths(AccessLink selectedRow) => Settings.IndexSearchExcludedSubdirectoryPaths.Remove(selectedRow);

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

        internal void UpdateActionKeyword(Settings.ActionKeyword modifiedActionKeyword, string newActionKeyword, string oldActionKeyword)
        {
            Context.API.RemoveActionKeyword(Context.CurrentPluginMetadata.ID, oldActionKeyword);
            Context.API.AddActionKeyword(Context.CurrentPluginMetadata.ID, newActionKeyword);
        }

        internal bool IsActionKeywordAlreadyAssigned(string newActionKeyword)
        {
            return Context.API.ActionKeywordRegistered(newActionKeyword);
        }

        internal bool IsNewActionKeywordGlobal(string newActionKeyword) => newActionKeyword == Query.GlobalPluginWildcardSign;
    }
}
