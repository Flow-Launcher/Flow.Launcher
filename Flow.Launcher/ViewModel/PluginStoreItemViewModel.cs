using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using Version = SemanticVersioning.Version;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginStoreItemViewModel : BaseModel
    {
        private readonly UserPlugin _newPlugin;
        private readonly PluginPair _oldPluginPair;

        public PluginStoreItemViewModel(UserPlugin plugin)
        {
            _newPlugin = plugin;
            _oldPluginPair = PluginManager.GetPluginForId(plugin.ID);
        }

        public string ID => _newPlugin.ID;
        public string Name => _newPlugin.Name;
        public string Description => _newPlugin.Description;
        public string Author => _newPlugin.Author;
        public string Version => _newPlugin.Version;
        public string Language => _newPlugin.Language;
        public string Website => _newPlugin.Website;
        public string UrlDownload => _newPlugin.UrlDownload;
        public string UrlSourceCode => _newPlugin.UrlSourceCode;
        public string IcoPath => _newPlugin.IcoPath;
        public DateTime? DateAdded => _newPlugin.DateAdded;
        public DateTime? UpdatedDate => _newPlugin.LatestReleaseDate;

        public bool LabelInstalled => _oldPluginPair != null;
        public bool LabelUpdate => LabelInstalled && new Version(_newPlugin.Version) > new Version(_oldPluginPair.Metadata.Version);

        internal const string None = "None";
        internal const string RecentlyUpdated = "RecentlyUpdated";
        internal const string NewRelease = "NewRelease";
        internal const string Installed = "Installed";

        public string InstallCategory => LabelInstalled ? Installed : None;

        public string DefaultCategory
        {
            get
            {
                string category = None;
                if (_newPlugin.LatestReleaseDate is not null && 
                    DateTime.Now - _newPlugin.LatestReleaseDate < TimeSpan.FromDays(7))
                {
                    category = RecentlyUpdated;
                }
                if (_newPlugin.DateAdded is not null && 
                    DateTime.Now - _newPlugin.DateAdded < TimeSpan.FromDays(7))
                {
                    category = NewRelease;
                }
                if (_oldPluginPair != null)
                {
                    category = Installed;
                }

                return category;
            }
        }

        [RelayCommand]
        private async Task ShowCommandQueryAsync(string action)
        {
            switch (action)
            {
                case "install":
                    await PluginInstaller.InstallPluginAndCheckRestartAsync(_newPlugin);
                    break;
                case "uninstall":
                    await PluginInstaller.UninstallPluginAndCheckRestartAsync(_oldPluginPair.Metadata);
                    break;
                case "update":
                    await PluginInstaller.UpdatePluginAndCheckRestartAsync(_newPlugin, _oldPluginPair.Metadata);
                    break;
                default:
                    break;
            }
        }
    }
}
