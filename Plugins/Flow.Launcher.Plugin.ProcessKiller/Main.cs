using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Flow.Launcher.Plugin.ProcessKiller.ViewModels;
using Flow.Launcher.Plugin.ProcessKiller.Views;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider
    {
        internal static PluginInitContext Context { get; private set; }

        private Settings _settings;

        private readonly ProcessHelper processHelper = new();

        private SettingsViewModel _viewModel;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings);
        }

        public List<Result> Query(Query query)
        {
            return CreateResultsFromQuery(query);
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_processkiller_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_processkiller_plugin_description();
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
                    Title = Localize.flowlauncher_plugin_processkiller_kill_instances(),
                    SubTitle = processPath,
                    Action = _ =>
                    {
                        foreach (var p in similarProcesses)
                        {
                            ProcessHelper.TryKill(p);
                        }

                        return true;
                    },
                    IcoPath = processPath
                });
            }

            return menuOptions;
        }

        private List<Result> CreateResultsFromQuery(Query query)
        {
            // Get all non-system processes
            var allProcessList = processHelper.GetMatchingProcesses();
            if (allProcessList.Count == 0)
            {
                return null;
            }

            // Filter processes based on search term
            var searchTerm = query.Search;
            var processlist = new List<ProcessResult>();
            var processWindowTitle =
                _settings.ShowWindowTitle || _settings.PutVisibleWindowProcessesTop ?
                ProcessHelper.GetProcessesWithNonEmptyWindowTitle() :
                [];
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                foreach (var p in allProcessList)
                {
                    var progressNameIdTitle = ProcessHelper.GetProcessNameIdTitle(p);

                    if (processWindowTitle.TryGetValue(p.Id, out var windowTitle))
                    {
                        // Add score to prioritize processes with visible windows
                        // Use window title for those processes if enabled
                        processlist.Add(new ProcessResult(
                            p,
                            _settings.PutVisibleWindowProcessesTop ? 200 : 0,
                            _settings.ShowWindowTitle ? windowTitle : progressNameIdTitle,
                            null,
                            progressNameIdTitle));
                    }
                    else
                    {
                        processlist.Add(new ProcessResult(
                            p,
                            0,
                            progressNameIdTitle,
                            null,
                            progressNameIdTitle));
                    }
                }
            }
            else
            {
                foreach (var p in allProcessList)
                {
                    var progressNameIdTitle = ProcessHelper.GetProcessNameIdTitle(p);

                    if (processWindowTitle.TryGetValue(p.Id, out var windowTitle))
                    {
                        // Get max score from searching process name, window title and process id
                        var windowTitleMatch = Context.API.FuzzySearch(searchTerm, windowTitle);
                        var processNameIdMatch = Context.API.FuzzySearch(searchTerm, progressNameIdTitle);
                        var score = Math.Max(windowTitleMatch.Score, processNameIdMatch.Score);
                        if (score > 0)
                        {
                            // Add score to prioritize processes with visible windows
                            // Use window title for those processes
                            if (_settings.PutVisibleWindowProcessesTop)
                            {
                                score += 200;
                            }
                            processlist.Add(new ProcessResult(
                                p,
                                score,
                                _settings.ShowWindowTitle ? windowTitle : progressNameIdTitle,
                                score == windowTitleMatch.Score ? windowTitleMatch : null,
                                progressNameIdTitle));
                        }
                    }
                    else
                    {
                        var processNameIdMatch = Context.API.FuzzySearch(searchTerm, progressNameIdTitle);
                        var score = processNameIdMatch.Score;
                        if (score > 0)
                        {
                            processlist.Add(new ProcessResult(
                                p,
                                score,
                                progressNameIdTitle,
                                processNameIdMatch,
                                progressNameIdTitle));
                        }
                    }
                }
            }

            var results = new List<Result>();
            foreach (var pr in processlist)
            {
                var p = pr.Process;
                var path = ProcessHelper.TryGetProcessFilename(p);
                results.Add(new Result()
                {
                    IcoPath = path,
                    Title = pr.Title,
                    TitleToolTip = pr.Tooltip,
                    SubTitle = path,
                    TitleHighlightData = pr.TitleMatch?.MatchData,
                    Score = pr.Score,
                    ContextData = p.ProcessName,
                    AutoCompleteText = $"{Context.CurrentPluginMetadata.ActionKeyword}{Plugin.Query.TermSeparator}{p.ProcessName}",
                    Action = (c) =>
                    {
                        ProcessHelper.TryKill(p);
                        // Re-query to refresh process list
                        Context.API.ReQuery();
                        return true;
                    }
                });
            }

            // Order results by process name for processes without visible windows
            var sortedResults = results.OrderBy(x => x.Title).ToList();

            // When there are multiple results AND all of them are instances of the same executable
            // add a quick option to kill them all at the top of the results.
            var firstResult = sortedResults.FirstOrDefault(x => !string.IsNullOrEmpty(x.SubTitle));
            if (processlist.Count > 1 && !string.IsNullOrEmpty(searchTerm) && sortedResults.All(r => r.SubTitle == firstResult?.SubTitle))
            {
                sortedResults.Insert(1, new Result()
                {
                    IcoPath = firstResult?.IcoPath,
                    Title = Localize.flowlauncher_plugin_processkiller_kill_all(firstResult?.ContextData),
                    SubTitle = Localize.flowlauncher_plugin_processkiller_kill_all_count(processlist.Count),
                    Score = 200,
                    Action = (c) =>
                    {
                        foreach (var p in processlist)
                        {
                            ProcessHelper.TryKill(p.Process);
                        }
                        // Re-query to refresh process list
                        Context.API.ReQuery();
                        return true;
                    }
                });
            }

            return sortedResults;
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_viewModel);
        }
    }
}
