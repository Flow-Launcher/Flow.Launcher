using System.Windows.Controls;
using AvaloniaControl = Avalonia.Controls.Control;

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

        /// <summary>
        /// Create settings panel control for Avalonia version
        /// </summary>
        /// <returns></returns>
        virtual AvaloniaControl CreateSettingPanelAvalonia() => null;
    }
}

