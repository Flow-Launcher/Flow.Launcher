namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Action Flow Launcher takes after a plugin is installed, updated or uninstalled.
    /// </summary>
    public enum PluginModifiedAction
    {
        /// <summary>
        /// Reload the plugin in place without restarting Flow Launcher.
        /// If the reload fails, the user is notified that a restart is required.
        /// </summary>
        HotReload,

        /// <summary>
        /// Restart Flow Launcher automatically to apply the change.
        /// </summary>
        AutoRestart,

        /// <summary>
        /// Do nothing; the user restarts Flow Launcher manually to apply the change.
        /// </summary>
        Manual
    }
}
