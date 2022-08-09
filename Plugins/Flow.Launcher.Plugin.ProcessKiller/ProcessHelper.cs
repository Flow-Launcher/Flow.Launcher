using Flow.Launcher.Infrastructure;
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

            int portNum;
            bool canConvert = int.TryParse(searchTerm, out portNum);
            Tuple<bool, PortDetail> tcpPortListeningProcess = null;
            if(canConvert)
                tcpPortListeningProcess = PortHelper.GetPortDetails(portNum);

            foreach (var p in Process.GetProcesses())
            {
                if (IsSystemProcess(p)) continue;
                if (tcpPortListeningProcess != null && tcpPortListeningProcess.Item1 && tcpPortListeningProcess.Item2.Process.Id == p.Id)
                {
                    continue;
                }
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
            if (tcpPortListeningProcess != null && tcpPortListeningProcess.Item1)
            {
                var p = tcpPortListeningProcess.Item2.Process;
                processlist.Add(new ProcessResult(p, StringMatcher.FuzzySearch(searchTerm, p.ProcessName + p.Id).Score, portNum));
            }
            return processlist;
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
                }
            }
            catch (Exception e)
            {
                Log.Exception($"|ProcessKiller.CreateResultsFromProcesses|Failed to kill process {p.ProcessName}", e);
                TryKillRunAs(p);
            }
        }

        public void TryKillRunAs(Process p)
        {
            try
            {
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.FileName = "powershell.exe";
                startInfo.Arguments = $"Start cmd.exe -ArgumentList \"/k\",\"taskkill\",\"/f\",\"/pid\", \"{p.Id}\" -Verb Runas";
                startInfo.UseShellExecute = false;
                process.StartInfo = startInfo;
                process.Start();
            }
            catch(Exception e)
            {
                Log.Exception($"|ProcessKiller.CreateResultsFromProcesses|Failed to kill process again of run as admin {p.ProcessName}", e);
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
