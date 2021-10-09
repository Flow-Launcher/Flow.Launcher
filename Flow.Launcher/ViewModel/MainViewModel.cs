using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Storage;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.VisualStudio.Threading;
using System.Threading.Channels;
using ISavable = Flow.Launcher.Plugin.ISavable;
using System.Windows.Threading;

namespace Flow.Launcher.ViewModel
{
    public class MainViewModel : BaseModel, ISavable
    {
        #region Private Fields

        private const string DefaultOpenResultModifiers = "Alt";

        private bool _isQueryRunning;
        private Query _lastQuery;
        private string _queryTextBeforeLeaveResults;

        private readonly FlowLauncherJsonStorage<History> _historyItemsStorage;
        private readonly FlowLauncherJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly FlowLauncherJsonStorage<TopMostRecord> _topMostRecordStorage;
        internal readonly Settings _settings;
        private readonly History _history;
        private readonly UserSelectedRecord _userSelectedRecord;
        private readonly TopMostRecord _topMostRecord;

        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;

        private readonly Internationalization _translator = InternationalizationManager.Instance;

        private ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
        private Task _resultsViewUpdateTask;

        #endregion

        #region Constructor

        public MainViewModel(Settings settings)
        {
            _queryTextBeforeLeaveResults = "";
            _queryText = "";
            _lastQuery = new Query();

            _settings = settings;

            _historyItemsStorage = new FlowLauncherJsonStorage<History>();
            _userSelectedRecordStorage = new FlowLauncherJsonStorage<UserSelectedRecord>();
            _topMostRecordStorage = new FlowLauncherJsonStorage<TopMostRecord>();
            _history = _historyItemsStorage.Load();
            _userSelectedRecord = _userSelectedRecordStorage.Load();
            _topMostRecord = _topMostRecordStorage.Load();

            ContextMenu = new ResultsViewModel(_settings);
            Results = new ResultsViewModel(_settings);
            History = new ResultsViewModel(_settings);
            _selectedResults = Results;

            InitializeKeyCommands();
            RegisterViewUpdate();
            RegisterResultsUpdatedEvent();

            SetOpenResultModifiers();
        }

        private void RegisterViewUpdate()
        {
            var resultUpdateChannel = Channel.CreateUnbounded<ResultsForUpdate>();
            _resultsUpdateChannelWriter = resultUpdateChannel.Writer;
            _resultsViewUpdateTask =
                Task.Run(updateAction).ContinueWith(continueAction, TaskContinuationOptions.OnlyOnFaulted);

            async Task updateAction()
            {
                var queue = new Dictionary<string, ResultsForUpdate>();
                var channelReader = resultUpdateChannel.Reader;

                // it is not supposed to be false because it won't be complete
                while (await channelReader.WaitToReadAsync())
                {
                    await Task.Delay(20);
                    while (channelReader.TryRead(out var item))
                    {
                        if (!item.Token.IsCancellationRequested)
                            queue[item.ID] = item;
                    }

                    UpdateResultView(queue.Values);
                    queue.Clear();
                }

                Log.Error("MainViewModel", "Unexpected ResultViewUpdate ends");
            };

            void continueAction(Task t)
            {
#if DEBUG
                throw t.Exception;
#else
                Log.Error($"Error happen in task dealing with viewupdate for results. {t.Exception}");
                _resultsViewUpdateTask =
                    Task.Run(updateAction).ContinueWith(continueAction, TaskContinuationOptions.OnlyOnFaulted);
#endif
            }
        }

        private void RegisterResultsUpdatedEvent()
        {
            foreach (var pair in PluginManager.GetPluginsForInterface<IResultUpdated>())
            {
                var plugin = (IResultUpdated)pair.Plugin;
                plugin.ResultsUpdated += (s, e) =>
                {
                    if (e.Query.RawQuery != QueryText || e.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    var token = e.Token == default ? _updateToken : e.Token;

                    PluginManager.UpdatePluginMetadata(e.Results, pair.Metadata, e.Query);
                    if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(e.Results, pair.Metadata, e.Query, token)))
                    {
                        Log.Error("MainViewModel", "Unable to add item to Result Update Queue");
                    }
                };
            }
        }


        private void InitializeKeyCommands()
        {
            EscCommand = new RelayCommand(_ =>
            {
                if (!SelectedIsFromQueryResults())
                {
                    SelectedResults = Results;
                }
                else
                {
                    MainWindowVisibility = Visibility.Collapsed;
                }
            });

            SelectNextItemCommand = new RelayCommand(_ => { SelectedResults.SelectNextResult(); });

            SelectPrevItemCommand = new RelayCommand(_ => { SelectedResults.SelectPrevResult(); });

            SelectNextPageCommand = new RelayCommand(_ => { SelectedResults.SelectNextPage(); });

            SelectPrevPageCommand = new RelayCommand(_ => { SelectedResults.SelectPrevPage(); });

            SelectFirstResultCommand = new RelayCommand(_ => SelectedResults.SelectFirstResult());

            StartHelpCommand = new RelayCommand(_ =>
            {
                SearchWeb.NewTabInBrowser("https://github.com/Flow-Launcher/Flow.Launcher/wiki/Flow-Launcher/");
            });

            OpenResultCommand = new RelayCommand(index =>
            {
                var results = SelectedResults;

                if (index != null)
                {
                    results.SelectedIndex = int.Parse(index.ToString());
                }

                var result = results.SelectedItem?.Result;
                if (result != null) // SelectedItem returns null if selection is empty.
                {
                    bool hideWindow = result.Action != null && result.Action(new ActionContext
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });

                    if (hideWindow)
                    {
                        MainWindowVisibility = Visibility.Collapsed;
                    }

                    if (SelectedIsFromQueryResults())
                    {
                        _userSelectedRecord.Add(result);
                        _history.Add(result.OriginQuery.RawQuery);
                    }
                    else
                    {
                        SelectedResults = Results;
                    }
                }
            });

            LoadContextMenuCommand = new RelayCommand(_ =>
            {
                if (SelectedIsFromQueryResults())
                {
                    // When switch to ContextMenu from QueryResults, but no item being chosen, should do nothing
                    // i.e. Shift+Enter/Ctrl+O right after Alt + Space should do nothing
                    if (SelectedResults.SelectedItem != null)
                        SelectedResults = ContextMenu;
                }
                else
                {
                    SelectedResults = Results;
                }
            });

            LoadHistoryCommand = new RelayCommand(_ =>
            {
                if (SelectedIsFromQueryResults())
                {
                    SelectedResults = History;
                    History.SelectedIndex = _history.Items.Count - 1;
                }
                else
                {
                    SelectedResults = Results;
                }
            });

            ReloadPluginDataCommand = new RelayCommand(_ =>
            {
                var msg = new Msg
                {
                    Owner = Application.Current.MainWindow
                };

                MainWindowVisibility = Visibility.Collapsed;

                PluginManager
                    .ReloadData()
                    .ContinueWith(_ =>
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            msg.Show(
                                InternationalizationManager.Instance.GetTranslation("success"),
                                InternationalizationManager.Instance.GetTranslation("completedSuccessfully"),
                                "");
                        }))
                    .ConfigureAwait(false);
            });
        }

        #endregion

        #region ViewModel Properties

        public ResultsViewModel Results { get; private set; }
        public ResultsViewModel ContextMenu { get; private set; }
        public ResultsViewModel History { get; private set; }

        private string _queryText;

        public string QueryText
        {
            get => _queryText;
            set
            {
                _queryText = value;
                Query();
            }
        }

        /// <summary>
        /// we need move cursor to end when we manually changed query
        /// but we don't want to move cursor to end when query is updated from TextBox
        /// </summary>
        /// <param name="queryText"></param>
        public void ChangeQueryText(string queryText, bool reQuery = false)
        {
            if (QueryText!=queryText) 
            {
                // re-query is done in QueryText's setter method
                QueryText = queryText;
            }
            else if (reQuery)
            {
                Query();
            }
            QueryTextCursorMovedToEnd = true;
        }

        public bool LastQuerySelected { get; set; }

        // This is not a reliable indicator of the cursor's position, it is manually set for a specific purpose.
        public bool QueryTextCursorMovedToEnd { get; set; }

        private ResultsViewModel _selectedResults;

        private ResultsViewModel SelectedResults
        {
            get { return _selectedResults; }
            set
            {
                _selectedResults = value;
                if (SelectedIsFromQueryResults())
                {
                    ContextMenu.Visbility = Visibility.Collapsed;
                    History.Visbility = Visibility.Collapsed;
                    ChangeQueryText(_queryTextBeforeLeaveResults);
                }
                else
                {
                    Results.Visbility = Visibility.Collapsed;
                    _queryTextBeforeLeaveResults = QueryText;


                    // Because of Fody's optimization
                    // setter won't be called when property value is not changed.
                    // so we need manually call Query()
                    // http://stackoverflow.com/posts/25895769/revisions
                    if (string.IsNullOrEmpty(QueryText))
                    {
                        Query();
                    }
                    else
                    {
                        QueryText = string.Empty;
                    }
                }

                _selectedResults.Visbility = Visibility.Visible;
            }
        }

        public Visibility ProgressBarVisibility { get; set; }

        public Visibility MainWindowVisibility { get; set; }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }
        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand SelectFirstResultCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand LoadContextMenuCommand { get; set; }
        public ICommand LoadHistoryCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }
        public ICommand ReloadPluginDataCommand { get; set; }

        public string OpenResultCommandModifiers { get; private set; }

        public string Image => Constant.QueryTextBoxIconImagePath;

        #endregion

        public void Query()
        {
            if (SelectedIsFromQueryResults())
            {
                QueryResults();
            }
            else if (ContextMenuSelected())
            {
                QueryContextMenu();
            }
            else if (HistorySelected())
            {
                QueryHistory();
            }
        }

        private void QueryContextMenu()
        {
            const string id = "Context Menu ID";
            var query = QueryText.ToLower().Trim();
            ContextMenu.Clear();

            var selected = Results.SelectedItem?.Result;

            if (selected != null) // SelectedItem returns null if selection is empty.
            {
                var results = PluginManager.GetContextMenusForPlugin(selected);
                results.Add(ContextMenuTopMost(selected));
                results.Add(ContextMenuPluginInfo(selected.PluginID));

                if (!string.IsNullOrEmpty(query))
                {
                    var filtered = results.Where
                    (
                        r =>
                        {
                            var match = StringMatcher.FuzzySearch(query, r.Title);
                            if (!match.IsSearchPrecisionScoreMet())
                            {
                                match = StringMatcher.FuzzySearch(query, r.SubTitle);
                            }

                            if (!match.IsSearchPrecisionScoreMet()) return false;

                            r.Score = match.Score;
                            return true;

                        }).ToList();
                    ContextMenu.AddResults(filtered, id);
                }
                else
                {
                    ContextMenu.AddResults(results, id);
                }
            }
        }

        private void QueryHistory()
        {
            const string id = "Query History ID";
            var query = QueryText.ToLower().Trim();
            History.Clear();

            var results = new List<Result>();
            foreach (var h in _history.Items)
            {
                var title = _translator.GetTranslation("executeQuery");
                var time = _translator.GetTranslation("lastExecuteTime");
                var result = new Result
                {
                    Title = string.Format(title, h.Query),
                    SubTitle = string.Format(time, h.ExecutedDateTime),
                    IcoPath = "Images\\history.png",
                    OriginQuery = new Query
                    {
                        RawQuery = h.Query
                    },
                    Action = _ =>
                    {
                        SelectedResults = Results;
                        ChangeQueryText(h.Query);
                        return false;
                    }
                };
                results.Add(result);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var filtered = results.Where
                (
                    r => StringMatcher.FuzzySearch(query, r.Title).IsSearchPrecisionScoreMet() ||
                         StringMatcher.FuzzySearch(query, r.SubTitle).IsSearchPrecisionScoreMet()
                ).ToList();
                History.AddResults(filtered, id);
            }
            else
            {
                History.AddResults(results, id);
            }
        }

        private readonly IReadOnlyList<Result> _emptyResult = new List<Result>();

        private async void QueryResults()
        {
            _updateSource?.Cancel();

            if (string.IsNullOrWhiteSpace(QueryText))
            {
                Results.Clear();
                Results.Visbility = Visibility.Collapsed;
                return;
            }

            _updateSource?.Dispose();

            var currentUpdateSource = new CancellationTokenSource();
            _updateSource = currentUpdateSource;
            var currentCancellationToken = _updateSource.Token;
            _updateToken = currentCancellationToken;

            ProgressBarVisibility = Visibility.Hidden;
            _isQueryRunning = true;

            // Switch to ThreadPool thread
            await TaskScheduler.Default;

            if (currentCancellationToken.IsCancellationRequested)
                return;

            var query = QueryBuilder.Build(QueryText.Trim(), PluginManager.NonGlobalPlugins);

            // handle the exclusiveness of plugin using action keyword
            RemoveOldQueryResults(query);

            _lastQuery = query;

            var plugins = PluginManager.ValidPluginsForQuery(query);

            if (query.ActionKeyword == Plugin.Query.GlobalPluginWildcardSign)
            {
                // Wait 45 millisecond for query change in global query
                // if query changes, return so that it won't be calculated
                await Task.Delay(45, currentCancellationToken);
                if (currentCancellationToken.IsCancellationRequested)
                    return;
            }

            _ = Task.Delay(200, currentCancellationToken).ContinueWith(_ =>
            {
                // start the progress bar if query takes more than 200 ms and this is the current running query and it didn't finish yet
                if (!currentCancellationToken.IsCancellationRequested && _isQueryRunning)
                {
                    ProgressBarVisibility = Visibility.Visible;
                }
            }, currentCancellationToken, TaskContinuationOptions.NotOnCanceled, TaskScheduler.Default);

            // plugins is ICollection, meaning LINQ will get the Count and preallocate Array

            var tasks = plugins.Select(plugin => plugin.Metadata.Disabled switch
            {
                false => QueryTask(plugin),
                true => Task.CompletedTask
            }).ToArray();


            try
            {
                // Check the code, WhenAll will translate all type of IEnumerable or Collection to Array, so make an array at first
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // nothing to do here
            }

            if (currentCancellationToken.IsCancellationRequested)
                return;

            // this should happen once after all queries are done so progress bar should continue
            // until the end of all querying
            _isQueryRunning = false;
            if (!currentCancellationToken.IsCancellationRequested)
            {
                // update to hidden if this is still the current query
                ProgressBarVisibility = Visibility.Hidden;
            }

            // Local function
            async Task QueryTask(PluginPair plugin)
            {
                // Since it is wrapped within a ThreadPool Thread, the synchronous context is null
                // Task.Yield will force it to run in ThreadPool
                await Task.Yield();

                IReadOnlyList<Result> results = await PluginManager.QueryForPluginAsync(plugin, query, currentCancellationToken);

                currentCancellationToken.ThrowIfCancellationRequested();

                results ??= _emptyResult;

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(results, plugin.Metadata, query, currentCancellationToken)))
                {
                    Log.Error("MainViewModel", "Unable to add item to Result Update Queue");
                }
            }
        }

        private void RemoveOldQueryResults(Query query)
        {
            if (_lastQuery.ActionKeyword != query.ActionKeyword)
            {
                Results.Clear();
            }
        }

        private Result ContextMenuTopMost(Result result)
        {
            Result menu;
            if (_topMostRecord.IsTopMost(result))
            {
                menu = new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("cancelTopMostInThisQuery"),
                    IcoPath = "Images\\down.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.Remove(result);
                        App.API.ShowMsg("Success");
                        return false;
                    }
                };
            }
            else
            {
                menu = new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("setAsTopMostInThisQuery"),
                    IcoPath = "Images\\up.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xeac2"),
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.AddOrUpdate(result);
                        App.API.ShowMsg("Success");
                        return false;
                    }
                };
            }

            return menu;
        }

        private Result ContextMenuPluginInfo(string id)
        {
            var metadata = PluginManager.GetPluginForId(id).Metadata;
            var translator = InternationalizationManager.Instance;

            var author = translator.GetTranslation("author");
            var website = translator.GetTranslation("website");
            var version = translator.GetTranslation("version");
            var plugin = translator.GetTranslation("plugin");
            var title = $"{plugin}: {metadata.Name}";
            var icon = metadata.IcoPath;
            var subtitle = $"{author}: {metadata.Author}, {website}: {metadata.Website} {version}: {metadata.Version}";

            var menu = new Result
            {
                Title = title,
                IcoPath = icon,
                SubTitle = subtitle,
                PluginDirectory = metadata.PluginDirectory,
                Action = _ => false
            };
            return menu;
        }

        internal bool SelectedIsFromQueryResults()
        {
            var selected = SelectedResults == Results;
            return selected;
        }

        private bool ContextMenuSelected()
        {
            var selected = SelectedResults == ContextMenu;
            return selected;
        }

        private bool HistorySelected()
        {
            var selected = SelectedResults == History;
            return selected;
        }

        #region Hotkey

        private void SetOpenResultModifiers()
        {
            OpenResultCommandModifiers = _settings.OpenResultModifiers ?? DefaultOpenResultModifiers;
        }

        internal void ToggleFlowLauncher()
        {
            if (MainWindowVisibility != Visibility.Visible)
            {
                MainWindowVisibility = Visibility.Visible;
            }
            else
            {
                MainWindowVisibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Public Methods

        public void Save()
        {
            _historyItemsStorage.Save();
            _userSelectedRecordStorage.Save();
            _topMostRecordStorage.Save();
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void UpdateResultView(IEnumerable<ResultsForUpdate> resultsForUpdates)
        {
            if (!resultsForUpdates.Any())
                return;
            CancellationToken token;

            try
            {
                // Don't know why sometimes even resultsForUpdates is empty, the method won't return;
                token = resultsForUpdates.Select(r => r.Token).Distinct().SingleOrDefault();
            }
#if DEBUG
            catch
            {
                throw new ArgumentException("Unacceptable token");
            }
#else
            catch
            {
                token = default;
            }
#endif

            foreach (var metaResults in resultsForUpdates)
            {
                foreach (var result in metaResults.Results)
                {
                    if (_topMostRecord.IsTopMost(result))
                    {
                        result.Score = int.MaxValue;
                    }
                    else
                    {
                        var priorityScore = metaResults.Metadata.Priority * 150;
                        result.Score += _userSelectedRecord.GetSelectedCount(result) + priorityScore;
                    }
                }
            }

            Results.AddResults(resultsForUpdates, token);
        }

        #endregion
    }
}