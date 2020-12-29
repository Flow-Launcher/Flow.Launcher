using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class Settings
    {
        internal string HotKeyInstall { get; set; } = "install";
        internal string HotkeyUninstall { get; set; } = "uninstall";

        internal string HotkeyUpdate { get; set; } = "update";
    }
}
