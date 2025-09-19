using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessHelper
    {
        private static readonly string ClassName = nameof(ProcessHelper);

        private readonly HashSet<string> _systemProcessList = new()
        {
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
        };

        private const string FlowLauncherProcessName = "Flow.Launcher";

        private bool IsSystemProcessOrFlowLauncher(Process p) => 
            _systemProcessList.Contains(p.ProcessName.ToLower()) ||
            string.Equals(p.ProcessName, FlowLauncherProcessName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Get title based on process name and id
        /// </summary>
        public static string GetProcessNameIdTitle(Process p)
        {
            var sb = new StringBuilder();
            sb.Append(p.ProcessName);
            sb.Append(" - ");
            sb.Append(p.Id);
            return sb.ToString();
        }

        /// <summary>
        /// Returns a Process for evey running non-system process
        /// </summary>
        public List<Process> GetMatchingProcesses()
        {
            var processlist = new List<Process>();

            foreach (var p in Process.GetProcesses())
            {
                if (IsSystemProcessOrFlowLauncher(p)) continue;

                processlist.Add(p);
            }

            return processlist;
        }

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

        /// <summary>
        /// Returns all non-system processes whose file path matches the given processPath
        /// </summary>
        public IEnumerable<Process> GetSimilarProcesses(string processPath)
        {
            return Process.GetProcesses().Where(p => !IsSystemProcessOrFlowLauncher(p) && TryGetProcessFilename(p) == processPath);
        }

        public void TryKill(PluginInitContext context, Process p)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(50);
                }
            }
            catch (Exception e)
            {
                context.API.LogException(ClassName, $"Failed to kill process {p.ProcessName}", e);
            }
        }

        public unsafe string TryGetProcessFilename(Process p)
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
