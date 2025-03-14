using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    public class Main : IPlugin, IPluginI18n, IContextMenu
    {
        private readonly ProcessHelper processHelper = new();

        private static PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            return CreateResultsFromQuery(query);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_processkiller_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_processkiller_plugin_description");
        }

        public List<Result> LoadContextMenus(Result result)
        {
            var menuOptions = new List<Result>();
            var processPath = result.SubTitle;

            // get all non-system processes whose file path matches that of the given result (processPath)
            var similarProcesses = processHelper.GetSimilarProcesses(processPath);

            if (similarProcesses.Any())
            {
                menuOptions.Add(new Result
                {
                    Title = _context.API.GetTranslation("flowlauncher_plugin_processkiller_kill_instances"),
                    SubTitle = processPath,
                    Action = _ =>
                    {
                        foreach (var p in similarProcesses)
                        {
                            processHelper.TryKill(p);
                        }

                        return true;
                    },
                    IcoPath = processPath
                });
            }

            return menuOptions;
        }

        private record RunningProcessInfo(string ProcessName, string MainWindowTitle);

        private List<Result> CreateResultsFromQuery(Query query)
        {
            var searchTerm = query.Search;
            var processWindowTitle = ProcessHelper.GetProcessesWithNonEmptyWindowTitle();
            var processList = processHelper.GetMatchingProcesses(searchTerm, processWindowTitle);

            if (!processList.Any())
            {
                return null;
            }

            var results = new List<Result>();
            foreach (var pr in processList)
            {
                var p = pr.Process;
                var path = processHelper.TryGetProcessFilename(p);
                var title = p.ProcessName + " - " + p.Id;
                var score = pr.Score;
                if (processWindowTitle.TryGetValue(p.Id, out var mainWindowTitle))
                {
                    title = mainWindowTitle;
                    if (string.IsNullOrWhiteSpace(searchTerm))
                    {
                        // Add score to prioritize processes with visible windows
                        score += 200;
                    }
                }
                results.Add(new Result()
                {
                    IcoPath = path,
                    Title = title,
                    SubTitle = path,
                    TitleHighlightData = StringMatcher.FuzzySearch(searchTerm, p.ProcessName).MatchData,
                    Score = score,
                    ContextData = new RunningProcessInfo(p.ProcessName, mainWindowTitle),
                    AutoCompleteText = $"{_context.CurrentPluginMetadata.ActionKeyword}{Plugin.Query.TermSeparator}{p.ProcessName}",
                    Action = (c) =>
                    {
                        processHelper.TryKill(p);
                        _context.API.ReQuery();
                        return false;
                    }
                });
            }

            var sortedResults = results
                .OrderBy(x => x.Title)
                .ToList();

            // When there are multiple results AND all of them are instances of the same executable
            // add a quick option to kill them all at the top of the results.
            var firstResult = sortedResults.FirstOrDefault(x => !string.IsNullOrEmpty(x.SubTitle));
            if (processList.Count > 1 && !string.IsNullOrEmpty(searchTerm) && sortedResults.All(r => r.SubTitle == firstResult?.SubTitle))
            {
                sortedResults.Insert(1, new Result()
                {
                    IcoPath = firstResult?.IcoPath,
                    Title = string.Format(_context.API.GetTranslation("flowlauncher_plugin_processkiller_kill_all"), ((RunningProcessInfo)firstResult?.ContextData).ProcessName),
                    SubTitle = string.Format(_context.API.GetTranslation("flowlauncher_plugin_processkiller_kill_all_count"), processList.Count),
                    Score = 200,
                    Action = (c) =>
                    {
                        foreach (var p in processList)
                        {
                            processHelper.TryKill(p.Process);
                        }
                        _context.API.ReQuery();
                        return false;
                    }
                });
            }

            return sortedResults;
        }
    }
}
