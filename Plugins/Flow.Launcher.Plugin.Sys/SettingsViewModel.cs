namespace Flow.Launcher.Plugin.Sys
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
