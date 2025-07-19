using System.Windows.Controls;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// This interface is used to create settings panel for .Net plugins
    /// </summary>
    public interface ISettingProvider
    {
        /// <summary>
        /// Create settings panel control for .Net plugins
        /// </summary>
        /// <returns></returns>
        Control CreateSettingPanel();
    }
}
