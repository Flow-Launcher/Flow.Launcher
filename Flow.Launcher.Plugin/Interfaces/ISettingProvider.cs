namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// This interface is used to create settings panel for .Net plugins
    /// </summary>
    public partial interface ISettingProvider
    {
        /// <summary>
        /// Create settings panel control for Avalonia version
        /// </summary>
        /// <returns></returns>
        virtual object CreateSettingPanelAvalonia() => null;
    }
}
