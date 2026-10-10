using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal partial class ProcessHelper
    {
        private static readonly string ClassName = nameof(ProcessHelper);

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
        /// Returns all non-system processes whose file path matches the given processPath
        /// </summary>
        public IEnumerable<Process> GetSimilarProcesses(string processPath)
        {
            return Process.GetProcesses().Where(p => !IsSystemProcessOrFlowLauncher(p) && TryGetProcessFilename(p) == processPath);
        }

        public static void TryKill(Process p)
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
                Main.Context.API.LogException(ClassName, $"Failed to kill process {p.ProcessName}", e);
            }
        }
    }
}
