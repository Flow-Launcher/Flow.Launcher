using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Plugin.PluginsManager.ViewModels
{
    internal class SettingsViewModel
    {
        internal Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public SettingsViewModel(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }
    }
}
