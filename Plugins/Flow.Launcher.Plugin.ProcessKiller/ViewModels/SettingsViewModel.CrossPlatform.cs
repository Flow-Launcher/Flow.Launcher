namespace Flow.Launcher.Plugin.ProcessKiller.ViewModels
{
    public partial class SettingsViewModel
    {
        /// <summary>
        /// Window title settings rely on Win32 window enumeration, which is not available here.
        /// </summary>
        public bool ShowWindowTitleSettings => false;
    }
}
