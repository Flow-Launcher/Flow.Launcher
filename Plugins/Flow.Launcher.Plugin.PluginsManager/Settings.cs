namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class Settings
    {
        internal const string InstallCommand = "install";

        internal const string UninstallCommand = "uninstall";

        internal const string UpdateCommand = "update";

        public bool WarnFromUnknownSource { get; set; } = true;
        
        public bool AutoRestartAfterChanging { get; set; } = true;
    }
}
