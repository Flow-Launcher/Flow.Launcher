using System;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class PluginStoreItemViewModel : BaseModel
    {
        public PluginStoreItemViewModel(UserPlugin plugin)
        {
            _plugin = plugin;
        }

        private UserPlugin _plugin;

        public string Name => _plugin.Name;
        public string Description => _plugin.Description;
        public string Author => _plugin.Author;
        public string Version => _plugin.Version;


        public bool LabelNew => _plugin.LatestReleaseDate-DateTime.Now < TimeSpan.FromDays(7);
        public bool LabelInstalled => PluginManager.GetPluginForId(_plugin.ID) != null;
        public bool LabelUpdated => _plugin.DateAdded -DateTime.Now < TimeSpan.FromDays(7);
    }
}
