﻿using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessHelper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private readonly HashSet<string> _systemProcessList = new HashSet<string>()
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
        public Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle()
        {
            var processDict = new Dictionary<int, string>();
            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder windowTitle = new StringBuilder();
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);

                if (!string.IsNullOrWhiteSpace(windowTitle.ToString()) && IsWindowVisible(hWnd))
                {
                    GetWindowThreadProcessId(hWnd, out var processId);
                    var process = Process.GetProcessById((int)processId);

                    if (!processDict.ContainsKey((int)processId))
                    {
                        processDict.Add((int)processId, windowTitle.ToString());
                    }
                }

                return true;
            }, IntPtr.Zero);

            return processDict;
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

        public string TryGetProcessFilename(Process p)
        {
            try
            {
                int capacity = 2000;
                StringBuilder builder = new StringBuilder(capacity);
                IntPtr ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
                if (!QueryFullProcessImageName(ptr, 0, builder, ref capacity))
                {
                    return String.Empty;
                }

                return builder.ToString();
            }
            catch
            {
                return "";
            }
        }

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(
            [In] IntPtr hProcess,
            [In] int dwFlags,
            [Out] StringBuilder lpExeName,
            ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            ProcessAccessFlags processAccess,
            bool bInheritHandle,
            int processId);
    }
}
