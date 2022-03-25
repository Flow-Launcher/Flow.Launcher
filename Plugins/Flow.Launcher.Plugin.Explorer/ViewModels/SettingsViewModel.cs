using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using System.Diagnostics;
using System.Threading.Tasks;

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

        internal void UpdateActionKeyword(Settings.ActionKeyword modifiedActionKeyword, string newActionKeyword, string oldActionKeyword)
        {
            PluginManager.ReplaceActionKeyword(Context.CurrentPluginMetadata.ID, oldActionKeyword, newActionKeyword);
        }

        internal bool IsActionKeywordAlreadyAssigned(string newActionKeyword)
        {
            return PluginManager.ActionKeywordRegistered(newActionKeyword);
        }

        internal bool IsNewActionKeywordGlobal(string newActionKeyword) => newActionKeyword == Query.GlobalPluginWildcardSign;
    }
}
