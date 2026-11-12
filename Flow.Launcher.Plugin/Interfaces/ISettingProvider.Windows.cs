using System.Windows.Controls;

namespace Flow.Launcher.Plugin
{
    public partial interface ISettingProvider
    {
        /// <summary>
        /// Create settings panel control for .Net plugins
        /// </summary>
        /// <returns></returns>
        Control CreateSettingPanel();
    }
}
