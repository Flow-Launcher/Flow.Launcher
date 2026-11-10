using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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

        // Linux processes running as the user whose death ends or breaks the desktop session:
        // the user service manager, the session bus, display servers, compositors and session managers
        private readonly HashSet<string> _linuxSystemProcessList =
        [
            "systemd",
            "init",
            "dbus-daemon",
            "dbus-broker",
            "dbus-broker-launch",
            "xorg",
            "xwayland",
            "gnome-shell",
            "gnome-session-binary",
            "gnome-session-service",
            "plasmashell",
            "ksmserver",
            "kwin_wayland",
            "kwin_x11",
            "hyprland",
            "sway",
            "niri",
            "xfce4-session",
            "cinnamon-session",
            "mate-session"
        ];

        // The host executable name differs per platform and bundle, so Flow Launcher is matched by its own pid
        private bool IsSystemProcessOrFlowLauncher(Process p)
        {
            if (OperatingSystem.IsLinux())
            {
                return IsLinuxSystemProcessOrFlowLauncher(p);
            }

            return p.Id == Environment.ProcessId ||
                string.IsNullOrEmpty(p.ProcessName) ||
                _systemProcessList.Contains(p.ProcessName.ToLower());
        }

        /// <summary>
        /// /proc lists every process on the system. Only the user's own processes can be killed, and only their
        /// executable link is readable: kernel threads (kthreadd, kworker/…) have none and processes of other users
        /// (root daemons, init) deny access, so both are skipped along with the session's critical processes.
        /// </summary>
        private bool IsLinuxSystemProcessOrFlowLauncher(Process p)
        {
            if (p.Id == Environment.ProcessId || p.Id == 1 || string.IsNullOrEmpty(p.ProcessName))
            {
                return true;
            }

            var executablePath = GetLinuxExecutablePath(p.Id);
            if (string.IsNullOrEmpty(executablePath))
            {
                return true;
            }

            // The process name is the kernel's comm, cut to 15 characters (".Hyprland-wrapp"), so the executable's
            // file name is checked as well
            return _linuxSystemProcessList.Contains(NormalizeLinuxProcessName(p.ProcessName)) ||
                _linuxSystemProcessList.Contains(NormalizeLinuxProcessName(Path.GetFileName(executablePath)));
        }

        /// <summary>
        /// Nix wraps programs as ".name-wrapped" (e.g. ".Hyprland-wrapped"); match them by the program's own name.
        /// </summary>
        private static string NormalizeLinuxProcessName(string processName)
        {
            var name = processName.TrimStart('.').ToLowerInvariant();
            const string wrappedSuffix = "-wrapped";
            return name.EndsWith(wrappedSuffix, StringComparison.Ordinal) ? name[..^wrappedSuffix.Length] : name;
        }

        /// <summary>
        /// Window titles come from Win32 window enumeration, which is not available on this platform,
        /// so no process is reported as having a visible window.
        /// </summary>
        public static Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle() => [];

        public static string TryGetProcessFilename(Process p)
        {
            if (OperatingSystem.IsLinux())
            {
                return GetLinuxExecutablePath(p.Id);
            }

            try
            {
                return p.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Resolves /proc/{pid}/exe, a single readlink, where MainModule parses the whole /proc/{pid}/maps.
        /// Empty when the process has no executable (kernel thread, zombie) or belongs to another user.
        /// </summary>
        private static string GetLinuxExecutablePath(int pid)
        {
            try
            {
                var target = new FileInfo($"/proc/{pid}/exe").LinkTarget;
                if (string.IsNullOrEmpty(target))
                {
                    return string.Empty;
                }

                // The kernel marks executables replaced since the process started, e.g. by a package upgrade
                const string deletedSuffix = " (deleted)";
                return target.EndsWith(deletedSuffix, StringComparison.Ordinal) ? target[..^deletedSuffix.Length] : target;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }
    }
}
