using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using SemanticVersioning;
using Version = SemanticVersioning.Version;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginStoreItemViewModel : BaseModel
    {
        private PluginPair PluginManagerData => PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7");
        public PluginStoreItemViewModel(UserPlugin plugin)
        {
            _plugin = plugin;
        }

        private UserPlugin _plugin;

        public string ID => _plugin.ID;
        public string Name => _plugin.Name;
        public string Description => _plugin.Description;
        public string Author => _plugin.Author;
        public string Version => _plugin.Version;
        public string Language => _plugin.Language;
        public string Website => _plugin.Website;
        public string UrlDownload => _plugin.UrlDownload;
        public string UrlSourceCode => _plugin.UrlSourceCode;
        public string IcoPath => _plugin.IcoPath;

        public bool LabelInstalled => PluginManager.GetPluginForId(_plugin.ID) != null;
        public bool LabelUpdate => LabelInstalled && new Version(_plugin.Version) > new Version(PluginManager.GetPluginForId(_plugin.ID).Metadata.Version);

        internal const string None = "None";
        internal const string RecentlyUpdated = "RecentlyUpdated";
        internal const string NewRelease = "NewRelease";
        internal const string Installed = "Installed";

        public string Category
        {
            get
            {
                string category = None;
                if (DateTime.Now - _plugin.LatestReleaseDate < TimeSpan.FromDays(7))
                {
                    category = RecentlyUpdated;
                }
                if (DateTime.Now - _plugin.DateAdded < TimeSpan.FromDays(7))
                {
                    category = NewRelease;
                }
                if (PluginManager.GetPluginForId(_plugin.ID) != null)
                {
                    category = Installed;
                }

                return category;
            }
        }

        [RelayCommand]
        private void ShowCommandQuery(string action)
        {
            var actionKeyword = PluginManagerData.Metadata.ActionKeywords.Any() ? PluginManagerData.Metadata.ActionKeywords[0] + " " : String.Empty;
            App.API.ChangeQuery($"{actionKeyword}{action} {_plugin.Name}");
            App.API.ShowMainWindow();
        }
    }
}
