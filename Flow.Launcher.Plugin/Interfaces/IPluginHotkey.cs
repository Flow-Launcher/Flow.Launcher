using System.Collections.Generic;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Represent plugins that support global hotkey or search window hotkey.
    /// </summary>
    public interface IPluginHotkey : IFeatures
    {
        /// <summary>
        /// Get the list of plugin hotkeys which will be registered in the settings page.
        /// </summary>
        /// <returns></returns>
        List<BasePluginHotkey> GetPluginHotkeys();
    }
}
