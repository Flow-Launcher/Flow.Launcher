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

        public bool WarnFromUnknownSource 
        {
            get => Settings.WarnFromUnknownSource;
            set => Settings.WarnFromUnknownSource = value;
        }

        public bool AutoRestartAfterChanging
        { 
            get => Settings.AutoRestartAfterChanging;
            set => Settings.AutoRestartAfterChanging = value;
        }
    }
}
