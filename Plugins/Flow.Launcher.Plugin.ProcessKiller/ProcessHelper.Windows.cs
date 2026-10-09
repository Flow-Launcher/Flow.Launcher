using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal partial class ProcessHelper
    {
        private readonly HashSet<string> _systemProcessList =
        [
            "conhost",
            "svchost",
            "idle",
            "system",
            "rundll32",
            "csrss",
            "lsass",
            "lsm",
            "smss",
            "wininit",
            "winlogon",
            "services",
            "spoolsv",
            "explorer"
        ];

        private const string FlowLauncherProcessName = "Flow.Launcher";

        private bool IsSystemProcessOrFlowLauncher(Process p) =>
            _systemProcessList.Contains(p.ProcessName.ToLower()) ||
            string.Equals(p.ProcessName, FlowLauncherProcessName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns a dictionary of process IDs and their window titles for processes that have a visible main window with a non-empty title.
        /// </summary>
        public static unsafe Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle()
        {
            // Collect all window handles
            var windowHandles = new List<HWND>();
            PInvoke.EnumWindows((hWnd, _) =>
            {
                if (PInvoke.IsWindowVisible(hWnd))
                {
                    windowHandles.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            // Concurrently process each window handle
            var processDict = new ConcurrentDictionary<int, string>();
            var processedProcessIds = new ConcurrentDictionary<int, byte>();
            Parallel.ForEach(windowHandles, hWnd =>
            {
                var windowTitle = GetWindowTitle(hWnd);
                if (!string.IsNullOrWhiteSpace(windowTitle) && PInvoke.IsWindowVisible(hWnd))
                {
                    uint processId = 0;
                    var result = PInvoke.GetWindowThreadProcessId(hWnd, &processId);
                    if (result == 0u || processId == 0u)
                    {
                        return;
                    }

                    // Ensure each process ID is processed only once
                    if (processedProcessIds.TryAdd((int)processId, 0))
                    {
                        try
                        {
                            var process = Process.GetProcessById((int)processId);
                            processDict.TryAdd((int)processId, windowTitle);
                        }
                        catch
                        {
                            // Handle exceptions (e.g., process exited)
                        }
                    }
                }
            });

            return new Dictionary<int, string>(processDict);
        }

        private static unsafe string GetWindowTitle(HWND hwnd)
        {
            var capacity = PInvoke.GetWindowTextLength(hwnd) + 1;
            int length;
            Span<char> buffer = capacity < 1024 ? stackalloc char[capacity] : new char[capacity];
            fixed (char* pBuffer = buffer)
            {
                // If the window has no title bar or text, if the title bar is empty,
                // or if the window or control handle is invalid, the return value is zero.
                length = PInvoke.GetWindowText(hwnd, pBuffer, capacity);
            }

            return buffer[..length].ToString();
        }

        public static unsafe string TryGetProcessFilename(Process p)
        {
            try
            {
                var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)p.Id);
                if (handle == HWND.Null)
                {
                    return string.Empty;
                }

                using var safeHandle = new SafeProcessHandle((nint)handle.Value, true);
                uint capacity = 2000;
                Span<char> buffer = new char[capacity];
                if (!PInvoke.QueryFullProcessImageName(safeHandle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref capacity))
                {
                    return string.Empty;
                }

                return buffer[..(int)capacity].ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
