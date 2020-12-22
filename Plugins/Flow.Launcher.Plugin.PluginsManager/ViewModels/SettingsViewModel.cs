using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Plugin.PluginsManager.ViewModels
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
    }
}
