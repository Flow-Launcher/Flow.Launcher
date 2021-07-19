using Flow.Launcher.Infrastructure.Storage;

namespace Flow.Launcher.Plugin.WebSearch
{
    public class SettingsViewModel
    {
        private readonly PluginJsonStorage<Settings> _storage;

        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public Settings Settings { get; }

        public void Save()
        {
            _storage.Save();
        }
    }
}