using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessHelper
    {
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

        private bool IsSystemProcess(Process p) => _systemProcessList.Contains(p.ProcessName.ToLower());

        /// <summary>
        /// Returns a ProcessResult for evey running non-system process whose name matches the given searchTerm
        /// </summary>
        public List<ProcessResult> GetMatchingProcesses(string searchTerm)
        {
            var processlist = new List<ProcessResult>();

            foreach (var p in Process.GetProcesses())
            {
                if (IsSystemProcess(p)) continue;

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // show all non-system processes
                    processlist.Add(new ProcessResult(p, 0));
                }
                else
                {
                    var score = StringMatcher.FuzzySearch(searchTerm, p.ProcessName + p.Id).Score;
                    if (score > 0)
                    {
                        processlist.Add(new ProcessResult(p, score));
                    }
                }
            }

            return processlist;
        }

        /// <summary>
        /// Returns a dictionary of process IDs and their window titles for processes that have a visible main window with a non-empty title.
        /// </summary>
        public static unsafe Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle()
        {
            var processDict = new Dictionary<int, string>();
            PInvoke.EnumWindows((hWnd, lParam) =>
            {
                var windowTitle = GetWindowTitle(hWnd);
                if (!string.IsNullOrWhiteSpace(windowTitle) && PInvoke.IsWindowVisible(hWnd))
                {
                    uint processId = 0;
                    var result = PInvoke.GetWindowThreadProcessId(hWnd, &processId);
                    if (result == 0u || processId == 0u)
                    {
                        return false;
                    }

                    var process = Process.GetProcessById((int)processId);
                    if (!processDict.ContainsKey((int)processId))
                    {
                        processDict.Add((int)processId, windowTitle);
                    }
                }

                return true;
            }, IntPtr.Zero);

            return processDict;
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
            return Process.GetProcesses().Where(p => !IsSystemProcess(p) && TryGetProcessFilename(p) == processPath);
        }

        public void TryKill(Process p)
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
                Log.Exception($"{nameof(ProcessHelper)}", $"Failed to kill process {p.ProcessName}", e);
            }
        }

        public unsafe string TryGetProcessFilename(Process p)
        {
            try
            {
                var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)p.Id);
                if (handle.Value == IntPtr.Zero)
                {
                    return string.Empty;
                }

                using var safeHandle = new SafeProcessHandle(handle.Value, true);
                uint capacity = 2000;
                Span<char> buffer = new char[capacity];
                fixed (char* pBuffer = buffer)
                {
                    if (!PInvoke.QueryFullProcessImageName(safeHandle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, (PWSTR)pBuffer, ref capacity))
                    {
                        return string.Empty;
                    }

                    return buffer[..(int)capacity].ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
