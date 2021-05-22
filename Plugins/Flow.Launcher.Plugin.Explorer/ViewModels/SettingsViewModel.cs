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

        internal void UpdateActionKeyword(ActionKeywordProperty modifiedActionKeyword, string newActionKeyword, string oldActionKeyword)
        {
            if (Settings.SearchActionKeyword == Settings.PathSearchActionKeyword)
                PluginManager.AddActionKeyword(Context.CurrentPluginMetadata.ID, newActionKeyword);
            else
                PluginManager.ReplaceActionKeyword(Context.CurrentPluginMetadata.ID, oldActionKeyword, newActionKeyword);

            switch (modifiedActionKeyword)
            {
                case ActionKeywordProperty.SearchActionKeyword:
                    Settings.SearchActionKeyword = newActionKeyword;
                    break;
                case ActionKeywordProperty.PathSearchActionKeyword:
                    Settings.PathSearchActionKeyword = newActionKeyword;
                    break;
                case ActionKeywordProperty.FileContentSearchActionKeyword:
                    Settings.FileContentSearchActionKeyword = newActionKeyword;
                    break;
            }
        }

        internal bool IsActionKeywordAlreadyAssigned(string newActionKeyword) => PluginManager.ActionKeywordRegistered(newActionKeyword);

        internal bool IsNewActionKeywordGlobal(string newActionKeyword) => newActionKeyword == Query.GlobalPluginWildcardSign;
    }

    public enum ActionKeywordProperty
    {
        SearchActionKeyword,
        PathSearchActionKeyword,
        FileContentSearchActionKeyword
    }
}
