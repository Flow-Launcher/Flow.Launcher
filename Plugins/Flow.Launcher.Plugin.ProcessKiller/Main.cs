using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.InteropServices;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    public class Main : IPlugin
    {
        private readonly HashSet<string> _systemProcessList = new HashSet<string>(){
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
            "explorer"};

        public List<Result> Query(Query query)
        {
            var termToSearch = query.Terms.Length == 1
                ? null
                : query.FirstSearch.ToLower();
            var processlist = GetProcesslist(termToSearch);

            return
                !processlist.Any()
                    ? null
                    : CreateResultsFromProcesses(processlist, termToSearch);
        }

        private List<Result> CreateResultsFromProcesses(List<ProcessResult> processlist, string termToSearch)
        {
            var results = new List<Result>();

            foreach (var pr in processlist)
            {
                var p = pr.Process;
                var path = GetPath(p);
                results.Add(new Result()
                {
                    IcoPath = path,
                    Title = p.ProcessName + " - " + p.Id,
                    SubTitle = path,
                    Score = pr.Score,
                    Action = (c) =>
                    {
                        KillProcess(p);
                        return true;
                    }
                });
            }

            if (processlist.Count > 1 && !string.IsNullOrEmpty(termToSearch))
            {
                results.Insert(0, new Result()
                {
                    IcoPath = "Images\\app.png",
                    Title = "kill all \"" + termToSearch + "\" process",
                    SubTitle = "",
                    Score = 200,
                    Action = (c) =>
                    {
                        foreach (var p in processlist)
                        {
                            KillProcess(p.Process);
                        }

                        return true;
                    }
                });
            }

            return results;

            void KillProcess(Process p)
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
                    Log.Exception($"Fail to kill process {p.ProcessName}", e);
                }
            }
        }
        private List<ProcessResult> GetProcesslist(string termToSearch)
        {
            var processlist = new List<ProcessResult>();
            var processes = Process.GetProcesses();
            if (string.IsNullOrWhiteSpace(termToSearch))
            {
                // show all process
                foreach (var p in processes)
                {
                    if (FilterSystemProcesses(p)) continue;

                    processlist.Add(new ProcessResult(p,0));
                }
            }
            else
            {
                foreach (var p in processes)
                {
                    if (FilterSystemProcesses(p)) continue;
                    var score = StringMatcher.FuzzySearch(termToSearch, p.ProcessName + p.Id).Score;
                    if (score > 0)
                    {
                        processlist.Add(new ProcessResult(p, score));
                    }
                }
            }

            return processlist;

            bool FilterSystemProcesses(Process p)
            {
                var name = p.ProcessName.ToLower();
                if (_systemProcessList.Contains(name))
                    return true;
                return false;
            }
        }

        internal class ProcessResult
        {
            public ProcessResult(Process process, int score)
            {
                Process = process;
                Score = score;
            }

            public Process Process { get; }

            public int Score { get; }
        }

        private string GetPath(Process p)
        {
            try
            {
                var path = GetProcessFilename(p);
                return path.ToLower();
            }
            catch
            {
                return "";
            }
        }

        public void Init(PluginInitContext context)
        {
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

        private string GetProcessFilename(Process p)
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
    }
}
