namespace Flow.Launcher.Plugin.ProcessKiller.ViewModels
{
    public class SettingsViewModel
    {
        public Settings Settings { get; set; }

        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public bool ShowWindowTitle
        {
            get => Settings.ShowWindowTitle;
            set => Settings.ShowWindowTitle = value;
        }

        public bool PutVisibleWindowProcessesTop
        {
            get => Settings.PutVisibleWindowProcessesTop;
            set => Settings.PutVisibleWindowProcessesTop = value;
        }
    }
}
