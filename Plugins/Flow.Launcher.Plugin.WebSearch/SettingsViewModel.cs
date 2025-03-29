namespace Flow.Launcher.Plugin.WebSearch
{
    public class SettingsViewModel
    {
        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public Settings Settings { get; }
    }
}
