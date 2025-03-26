namespace Flow.Launcher.Plugin.ProcessKiller.ViewModels
{
    public class SettingsViewModel
    {
        public Settings Settings { get; set; }

        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public bool PutVisibleWindowProcessesTop
        {
            get => Settings.PutVisibleWindowProcessesTop;
            set => Settings.PutVisibleWindowProcessesTop = value;
        }
    }
}
