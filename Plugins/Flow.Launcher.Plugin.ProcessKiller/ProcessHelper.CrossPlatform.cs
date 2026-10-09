using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal partial class ProcessHelper
    {
        // macOS counterparts of the Windows list: the kernel, init, login session and desktop shell processes
        private readonly HashSet<string> _systemProcessList =
        [
            "kernel_task",
            "launchd",
            "loginwindow",
            "windowserver",
            "finder",
            "dock",
            "systemuiserver"
        ];

        // The host executable name differs per platform and bundle, so Flow Launcher is matched by its own pid
        private bool IsSystemProcessOrFlowLauncher(Process p) =>
            p.Id == Environment.ProcessId ||
            string.IsNullOrEmpty(p.ProcessName) ||
            _systemProcessList.Contains(p.ProcessName.ToLower());

        /// <summary>
        /// Window titles come from Win32 window enumeration, which is not available on this platform,
        /// so no process is reported as having a visible window.
        /// </summary>
        public static Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle() => [];

        public static string TryGetProcessFilename(Process p)
        {
            try
            {
                return p.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
