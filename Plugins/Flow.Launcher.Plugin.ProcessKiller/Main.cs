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
    public class Main : IPlugin, IPluginI18n
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

        private static PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

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

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_processkiller_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_processkiller_plugin_description");
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
                    TitleHighlightData = StringMatcher.FuzzySearch(termToSearch, p.ProcessName).MatchData,
                    Score = pr.Score,
                    Action = (c) =>
                    {
                        KillProcess(p);
                        return true;
                    }
                });
            }

            // When there are multiple results AND all of them are instances of the same executable
            // add a quick option to kill them all at the top of the results.
            var firstResult = results.FirstOrDefault()?.SubTitle;
            if (processlist.Count > 1 && !string.IsNullOrEmpty(termToSearch) && results.All(r => r.SubTitle == firstResult))
            {
                results.Insert(0, new Result()
                {
                    IcoPath = "Images\\app.png",
                    Title = string.Format(_context.API.GetTranslation("flowlauncher_plugin_processkiller_kill_all"), termToSearch),
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
                    Log.Exception($"|ProcessKiller.CreateResultsFromProcesses|Failed to kill process {p.ProcessName}", e);
                }
            }
        }
        private List<ProcessResult> GetProcesslist(string termToSearch)
        {
            var processlist = new List<ProcessResult>();

            foreach (var p in Process.GetProcesses())
            {
                if (FilterSystemProcesses(p)) continue;

                if (string.IsNullOrWhiteSpace(termToSearch))
                {
                    // show all non-system processes
                    processlist.Add(new ProcessResult(p,0));
                }
                else
                {
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
                return _systemProcessList.Contains(name);
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
                return GetProcessFilename(p);
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
