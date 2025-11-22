using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Storage;
using iNKORE.UI.WPF.Modern;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.ViewModel
{
    public partial class MainViewModel : BaseModel, ISavable, IDisposable, IResultUpdateRegister
    {
        #region Private Fields

        private static readonly string ClassName = nameof(MainViewModel);

        private Query _lastQuery;
        private bool _previousIsHomeQuery;
        private Query _progressQuery; // Used for QueryResultAsync
        private Query _updateQuery; // Used for ResultsUpdated
        private string _queryTextBeforeLeaveResults;
        private string _ignoredQueryText; // Used to ignore query text change when switching between context menu and query results

        private readonly FlowLauncherJsonStorage<History> _historyItemsStorage;
        private readonly FlowLauncherJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly FlowLauncherJsonStorageTopMostRecord _topMostRecord;
        private readonly History _history;
        private int lastHistoryIndex = 1;
        private readonly UserSelectedRecord _userSelectedRecord;

        private CancellationTokenSource _updateSource; // Used to cancel old query flows
        private CancellationToken _updateToken; // Used to avoid ObjectDisposedException of _updateSource.Token

        private ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
        private Task _resultsViewUpdateTask;

        private readonly IReadOnlyList<Result> _emptyResult = new List<Result>();
        private readonly IReadOnlyList<DialogJumpResult> _emptyDialogJumpResult = new List<DialogJumpResult>();

        private readonly PluginMetadata _historyMetadata = new()
        {
            ID = "298303A65D128A845D28A7B83B3968C2", // ID is for identifying the update plugin in UpdateActionAsync
            Priority = 0 // Priority is for calculating scores in UpdateResultView
        };

        #endregion

        #region Constructor

        public MainViewModel()
        {
            _queryTextBeforeLeaveResults = "";
            _queryText = "";
            _lastQuery = new Query();
            _ignoredQueryText = null; // null as invalid value

            Settings = Ioc.Default.GetRequiredService<Settings>();
            Settings.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Settings.WindowSize):
                        OnPropertyChanged(nameof(MainWindowWidth));
                        break;
                    case nameof(Settings.WindowHeightSize):
                        OnPropertyChanged(nameof(MainWindowHeight));
                        break;
                    case nameof(Settings.QueryBoxFontSize):
                        OnPropertyChanged(nameof(QueryBoxFontSize));
                        break;
                    case nameof(Settings.ItemHeightSize):
                        OnPropertyChanged(nameof(ItemHeightSize));
                        break;
                    case nameof(Settings.ResultItemFontSize):
                        OnPropertyChanged(nameof(ResultItemFontSize));
                        break;
                    case nameof(Settings.ResultSubItemFontSize):
                        OnPropertyChanged(nameof(ResultSubItemFontSize));
                        break;
                    case nameof(Settings.AlwaysStartEn):
                        OnPropertyChanged(nameof(StartWithEnglishMode));
                        break;
                    case nameof(Settings.OpenResultModifiers):
                        OnPropertyChanged(nameof(OpenResultCommandModifiers));
                        break;
                    case nameof(Settings.PreviewHotkey):
                        OnPropertyChanged(nameof(PreviewHotkey));
                        break;
                    case nameof(Settings.AutoCompleteHotkey):
                        OnPropertyChanged(nameof(AutoCompleteHotkey));
                        break;
                    case nameof(Settings.CycleHistoryUpHotkey):
                        OnPropertyChanged(nameof(CycleHistoryUpHotkey));
                        break;
                    case nameof(Settings.CycleHistoryDownHotkey):
                        OnPropertyChanged(nameof(CycleHistoryDownHotkey));
                        break;
                    case nameof(Settings.AutoCompleteHotkey2):
                        OnPropertyChanged(nameof(AutoCompleteHotkey2));
                        break;
                    case nameof(Settings.SelectNextItemHotkey):
                        OnPropertyChanged(nameof(SelectNextItemHotkey));
                        break;
                    case nameof(Settings.SelectNextItemHotkey2):
                        OnPropertyChanged(nameof(SelectNextItemHotkey2));
                        break;
                    case nameof(Settings.SelectPrevItemHotkey):
                        OnPropertyChanged(nameof(SelectPrevItemHotkey));
                        break;
                    case nameof(Settings.SelectPrevItemHotkey2):
                        OnPropertyChanged(nameof(SelectPrevItemHotkey2));
                        break;
                    case nameof(Settings.SelectNextPageHotkey):
                        OnPropertyChanged(nameof(SelectNextPageHotkey));
                        break;
                    case nameof(Settings.SelectPrevPageHotkey):
                        OnPropertyChanged(nameof(SelectPrevPageHotkey));
                        break;
                    case nameof(Settings.OpenContextMenuHotkey):
                        OnPropertyChanged(nameof(OpenContextMenuHotkey));
                        break;
                    case nameof(Settings.SettingWindowHotkey):
                        OnPropertyChanged(nameof(SettingWindowHotkey));
                        break;
                    case nameof(Settings.OpenHistoryHotkey):
                        OnPropertyChanged(nameof(OpenHistoryHotkey));
                        break;
                }
            };

            _historyItemsStorage = new FlowLauncherJsonStorage<History>();
            _userSelectedRecordStorage = new FlowLauncherJsonStorage<UserSelectedRecord>();
            _topMostRecord = new FlowLauncherJsonStorageTopMostRecord();
            _history = _historyItemsStorage.Load();
            _history.PopulateHistoryFromLegacyHistory();
            _userSelectedRecord = _userSelectedRecordStorage.Load();

            ContextMenu = new ResultsViewModel(Settings, this)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            Results = new ResultsViewModel(Settings, this)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            History = new ResultsViewModel(Settings, this)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            _selectedResults = Results;

            Results.PropertyChanged += (o, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Results.SelectedItem):
                        _selectedItemFromQueryResults = true;
                        PreviewSelectedItem = Results.SelectedItem;
                        _ = UpdatePreviewAsync();
                        break;
                }
            };

            History.PropertyChanged += (o, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(History.SelectedItem):
                        _selectedItemFromQueryResults = false;
                        PreviewSelectedItem = History.SelectedItem;
                        _ = UpdatePreviewAsync();
                        break;
                }
            };

            RegisterViewUpdate();
            _ = RegisterClockAndDateUpdateAsync();

            ThemeManager.Current.ActualApplicationThemeChanged += ThemeManager_ActualApplicationThemeChanged;
        }

        private void ThemeManager_ActualApplicationThemeChanged(ThemeManager sender, object args)
        {
            ActualApplicationThemeChanged?.Invoke(
                Application.Current,
                new ActualApplicationThemeChangedEventArgs()
                {
                    IsDark = sender.ActualApplicationTheme == ApplicationTheme.Dark
                });
        }

        private void RegisterViewUpdate()
        {
            var resultUpdateChannel = Channel.CreateUnbounded<ResultsForUpdate>();
            _resultsUpdateChannelWriter = resultUpdateChannel.Writer;
            _resultsViewUpdateTask =
                Task.Run(UpdateActionAsync).ContinueWith(continueAction,
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            async Task UpdateActionAsync()
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
                        {
                            // Indicate if to clear existing results so to show only ones from plugins with action keywords
                            var query = item.Query;
                            var currentIsHomeQuery = query.IsHomeQuery;
                            var shouldClearExistingResults = ShouldClearExistingResultsForQuery(query, currentIsHomeQuery);
                            _lastQuery = item.Query;
                            _previousIsHomeQuery = currentIsHomeQuery;

                            // If the queue already has the item, we need to pass the shouldClearExistingResults flag
                            if (queue.TryGetValue(item.ID, out var existingItem))
                            {
                                item.ShouldClearExistingResults = shouldClearExistingResults || existingItem.ShouldClearExistingResults;
                            }
                            else
                            {
                                item.ShouldClearExistingResults = shouldClearExistingResults;
                            }

                            queue[item.ID] = item;
                        }
                    }

                    UpdateResultView(queue.Values);
                    queue.Clear();
                }

                if (!_disposed)
                    App.API.LogError(ClassName, "Unexpected ResultViewUpdate ends");
            }

            void continueAction(Task t)
            {
#if DEBUG
                throw t.Exception;
#else
                App.API.LogError(ClassName, $"Error happen in task dealing with viewupdate for results. {t.Exception}");
                _resultsViewUpdateTask =
                    Task.Run(UpdateActionAsync).ContinueWith(continueAction, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
#endif
            }
        }

        public void RegisterResultsUpdatedEvent(PluginPair pair)
        {
            if (pair.Plugin is not IResultUpdated plugin) return;

            plugin.ResultsUpdated += (s, e) =>
            {
                if (_updateQuery == null || e.Query.Input != _updateQuery.Input || e.Token.IsCancellationRequested)
                {
                    return;
                }

                var token = e.Token == default ? _updateToken : e.Token;

                IReadOnlyList<Result> resultsCopy;
                if (e.Results == null)
                {
                    resultsCopy = _emptyResult;
                }
                else
                {
                    // make a clone to avoid possible issue that plugin will also change the list and items when updating view model
                    resultsCopy = DeepCloneResults(e.Results, false, token);
                }

                foreach (var result in resultsCopy)
                {
                    if (string.IsNullOrEmpty(result.BadgeIcoPath))
                    {
                        result.BadgeIcoPath = pair.Metadata.IcoPath;
                    }
                }

                PluginManager.UpdatePluginMetadata(resultsCopy, pair.Metadata, e.Query);

                if (token.IsCancellationRequested) return;

                App.API.LogDebug(ClassName, $"Update results for plugin <{pair.Metadata.Name}>");

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(resultsCopy, pair.Metadata, e.Query,
                    token)))
                {
                    App.API.LogError(ClassName, "Unable to add item to Result Update Queue");
                }
            };
        }

        private async Task RegisterClockAndDateUpdateAsync()
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            // ReSharper disable once MethodSupportsCancellation
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                if (Settings.UseClock)
                    ClockText = DateTime.Now.ToString(Settings.TimeFormat, CultureInfo.CurrentCulture);
                if (Settings.UseDate)
                    DateText = DateTime.Now.ToString(Settings.DateFormat, CultureInfo.CurrentCulture);
            }
        }

        [RelayCommand]
        private async Task ReloadPluginDataAsync()
        {
            Hide();

            await PluginManager.ReloadDataAsync().ConfigureAwait(false);
            App.API.ShowMsg(Localize.success(),
                Localize.completedSuccessfully());
        }

        [RelayCommand]
        private void LoadHistory()
        {
            if (QueryResultsSelected())
            {
                SelectedResults = History;
                History.SelectedIndex = _history.LastOpenedHistoryItems.Count - 1;
            }
            else
            {
                SelectedResults = Results;
            }
        }

        [RelayCommand]
        public void ReQuery()
        {
            if (QueryResultsSelected())
            {
                // When we are re-querying, we should not delay the query
                _ = QueryResultsAsync(false, isReQuery: true);
            }
        }

        public void ReQuery(bool reselect)
        {
            BackToQueryResults();
            // When we are re-querying, we should not delay the query
            _ = QueryResultsAsync(false, isReQuery: true, reSelect: reselect);
        }

        [RelayCommand]
        public void ReverseHistory()
        {
            var historyItems = _history.LastOpenedHistoryItems;
            if (historyItems.Count > 0)
            {
                ChangeQueryText(historyItems[^lastHistoryIndex].Query);
                if (lastHistoryIndex < historyItems.Count)
                {
                    lastHistoryIndex++;
                }
            }
        }

        [RelayCommand]
        public void ForwardHistory()
        {
            var historyItems = _history.LastOpenedHistoryItems;
            if (historyItems.Count > 0)
            {
                ChangeQueryText(historyItems[^lastHistoryIndex].Query);
                if (lastHistoryIndex > 1)
                {
                    lastHistoryIndex--;
                }
            }
        }

        [RelayCommand]
        private void LoadContextMenu()
        {
            // For Dialog Jump and right click mode, we need to navigate to the path
            if (_isDialogJump && Settings.DialogJumpResultBehaviour == DialogJumpResultBehaviours.RightClick)
            {
                if (SelectedResults.SelectedItem != null && DialogWindowHandle != nint.Zero)
                {
                    var result = SelectedResults.SelectedItem.Result;
                    if (result is DialogJumpResult dialogJumpResult)
                    {
                        Win32Helper.SetForegroundWindow(DialogWindowHandle);
                        _ = Task.Run(() => DialogJump.JumpToPathAsync(DialogWindowHandle, dialogJumpResult.DialogJumpPath));
                    }
                }
                return;
            }

            // For query mode, we load context menu
            if (QueryResultsSelected())
            {
                // When switch to ContextMenu from QueryResults, but no item being chosen, should do nothing
                // i.e. Shift+Enter/Ctrl+O right after Alt + Space should do nothing
                if (SelectedResults.SelectedItem != null)
                {
                    SelectedResults = ContextMenu;
                }
            }
            else
            {
                SelectedResults = Results;
            }
        }

        [RelayCommand]
        private void Backspace(object index)
        {
            var query = QueryBuilder.Build(QueryText, QueryText.Trim(), PluginManager.GetNonGlobalPlugins());

            // GetPreviousExistingDirectory does not require trailing '\', otherwise will return empty string
            var path = FilesFolders.GetPreviousExistingDirectory((_) => true, query.Search.TrimEnd('\\'));

            var actionKeyword = string.IsNullOrEmpty(query.ActionKeyword) ? string.Empty : $"{query.ActionKeyword} ";

            ChangeQueryText($"{actionKeyword}{path}");
        }

        [RelayCommand]
        private void AutocompleteQuery()
        {
            var result = SelectedResults.SelectedItem?.Result;
            if (result != null && QueryResultsSelected()) // SelectedItem returns null if selection is empty.
            {
                var autoCompleteText = result.Title;

                if (!string.IsNullOrEmpty(result.AutoCompleteText))
                {
                    autoCompleteText = result.AutoCompleteText;
                }
                else if (!string.IsNullOrEmpty(SelectedResults.SelectedItem?.QuerySuggestionText))
                {
                    //var defaultSuggestion = SelectedResults.SelectedItem.QuerySuggestionText;
                    //// check if result.actionkeywordassigned is empty
                    //if (!string.IsNullOrEmpty(result.ActionKeywordAssigned))
                    //{
                    //    autoCompleteText = $"{result.ActionKeywordAssigned} {defaultSuggestion}";
                    //}

                    autoCompleteText = SelectedResults.SelectedItem.QuerySuggestionText;
                }

                var specialKeyState = GlobalHotkey.CheckModifiers();
                if (specialKeyState.ShiftPressed)
                {
                    autoCompleteText = result.SubTitle;
                }

                ChangeQueryText(autoCompleteText);
            }
        }

        [RelayCommand]
        private async Task OpenResultAsync(string index)
        {
            // Must check query results selected before executing the action
            var queryResultsSelected = QueryResultsSelected();
            var results = SelectedResults;
            if (index is not null)
            {
                results.SelectedIndex = int.Parse(index);
            }

            var result = results.SelectedItem?.Result;
            if (result == null)
            {
                return;
            }

            // For Dialog Jump and left click mode, we need to navigate to the path
            if (_isDialogJump && Settings.DialogJumpResultBehaviour == DialogJumpResultBehaviours.LeftClick)
            {
                if (result is DialogJumpResult dialogJumpResult)
                {
                    Win32Helper.SetForegroundWindow(DialogWindowHandle);
                    _ = Task.Run(() => DialogJump.JumpToPathAsync(DialogWindowHandle, dialogJumpResult.DialogJumpPath));
                }
                else
                {
                    App.API.LogError(ClassName, "DialogJumpResult expected but got a different result type.");
                }
            }
            // For query mode, we execute the result
            else
            {
                var hideWindow = await result.ExecuteAsync(new ActionContext
                {
                    // not null means pressing modifier key + number, should ignore the modifier key
                    SpecialKeyState = index is not null ? SpecialKeyState.Default : GlobalHotkey.CheckModifiers()
                }).ConfigureAwait(false);

                if (hideWindow)
                {
                    Hide();
                }
            }

            // Record user selected result for result ranking
            _userSelectedRecord.Add(result);
            // Add item to history only if it is from results but not context menu or history
            if (queryResultsSelected)
            {
                _history.Add(result);
                lastHistoryIndex = 1;
            }
        }

        private static IReadOnlyList<Result> DeepCloneResults(IReadOnlyList<Result> results, bool isDialogJump, CancellationToken token = default)
        {
            var resultsCopy = new List<Result>();

            if (isDialogJump)
            {
                foreach (var result in results.ToList())
                {
                    if (token.IsCancellationRequested) break;

                    var resultCopy = ((DialogJumpResult)result).Clone();
                    resultsCopy.Add(resultCopy);
                }
            }
            else
            {
                foreach (var result in results.ToList())
                {
                    if (token.IsCancellationRequested) break;

                    var resultCopy = result.Clone();
                    resultsCopy.Add(resultCopy);
                }
            }

            return resultsCopy;
        }

        #endregion

        #region BasicCommands

        [RelayCommand]
        private void OpenSetting()
        {
            App.API.OpenSettingDialog();
        }

        [RelayCommand]
        private void SelectHelp()
        {
            App.API.OpenUrl("https://www.flowlauncher.com/docs/#/usage-tips");
        }

        [RelayCommand]
        private void SelectFirstResult()
        {
            SelectedResults.SelectFirstResult();
        }

        [RelayCommand]
        private void SelectLastResult()
        {
            SelectedResults.SelectLastResult();
        }

        [RelayCommand]
        private void SelectPrevPage()
        {
            SelectedResults.SelectPrevPage();
        }

        [RelayCommand]
        private void SelectNextPage()
        {
            SelectedResults.SelectNextPage();
        }

        [RelayCommand]
        private void SelectPrevItem()
        {
            var historyItems = _history.LastOpenedHistoryItems;
            if (QueryResultsSelected() // Results selected
                && string.IsNullOrEmpty(QueryText) // No input
                && Results.Visibility != Visibility.Visible // No items in result list, e.g. when home page is off and no query text is entered, therefore the view is collapsed.
                && historyItems.Count > 0) // Have history items
            {
                lastHistoryIndex = 1;
                ReverseHistory();
            }
            else
            {
                SelectedResults.SelectPrevResult();
            }
        }

        [RelayCommand]
        private void SelectNextItem()
        {
            SelectedResults.SelectNextResult();
        }

        [RelayCommand]
        private void Esc()
        {
            if (!QueryResultsSelected())
            {
                SelectedResults = Results;
            }
            else
            {
                Hide();
            }
        }

        public void BackToQueryResults()
        {
            if (!QueryResultsSelected())
            {
                SelectedResults = Results;
            }
        }

        [RelayCommand]
        public void ToggleGameMode()
        {
            GameModeStatus = !GameModeStatus;
        }

        [RelayCommand]
        public void CopyAlternative()
        {
            var result = Results.SelectedItem?.Result?.CopyText;

            if (result != null)
            {
                App.API.CopyToClipboard(result, directCopy: false);
            }
        }

        #endregion

        #region ViewModel Properties

        public Settings Settings { get; }
        public string ClockText { get; private set; }
        public string DateText { get; private set; }

        public ResultsViewModel Results { get; private set; }

        public ResultsViewModel ContextMenu { get; private set; }

        public ResultsViewModel History { get; private set; }

        public bool GameModeStatus { get; set; } = false;

        private string _queryText;
        public string QueryText
        {
            get => _queryText;
            set
            {
                _queryText = value;
                OnPropertyChanged();
            }
        }

        [RelayCommand]
        private void IncreaseWidth()
        {
            MainWindowWidth += 100;
            Settings.WindowLeft -= 50;
            OnPropertyChanged(nameof(MainWindowWidth));
        }

        [RelayCommand]
        private void DecreaseWidth()
        {
            if (MainWindowWidth - 100 < 400 || MainWindowWidth == 400)
            {
                MainWindowWidth = 400;
            }
            else
            {
                MainWindowWidth -= 100;
                Settings.WindowLeft += 50;
            }

            OnPropertyChanged(nameof(MainWindowWidth));
        }

        [RelayCommand]
        private void IncreaseMaxResult()
        {
            if (Settings.MaxResultsToShow == 17)
                return;

            Settings.MaxResultsToShow += 1;
        }

        [RelayCommand]
        private void DecreaseMaxResult()
        {
            if (Settings.MaxResultsToShow == 2)
                return;

            Settings.MaxResultsToShow -= 1;
        }

        /// <summary>
        /// we need move cursor to end when we manually changed query
        /// but we don't want to move cursor to end when query is updated from TextBox
        /// </summary>
        /// <param name="queryText"></param>
        /// <param name="isReQuery">Force query even when Query Text doesn't change</param>
        public void ChangeQueryText(string queryText, bool isReQuery = false)
        {
            // Must check access so that we will not block the UI thread which causes window visibility issue
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ChangeQueryText(queryText, isReQuery));
                return;
            }

            if (QueryText != queryText)
            {
                // Change query text first
                QueryText = queryText;
                // When we are changing query from codes, we should not delay the query
                Query(false, isReQuery: false);

                // set to false so the subsequent set true triggers
                // PropertyChanged and MoveQueryTextToEnd is called
                QueryTextCursorMovedToEnd = false;
            }
            else if (isReQuery)
            {
                // When we are re-querying, we should not delay the query
                Query(false, isReQuery: true);
            }

            QueryTextCursorMovedToEnd = true;
        }

        /// <summary>
        /// Async version of <see cref="ChangeQueryText"/>
        /// </summary>
        private async Task ChangeQueryTextAsync(string queryText, bool isReQuery = false)
        {
            // Must check access so that we will not block the UI thread which causes window visibility issue
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(() => ChangeQueryTextAsync(queryText, isReQuery));
                return;
            }

            if (QueryText != queryText)
            {
                // Change query text first
                QueryText = queryText;
                // When we are changing query from codes, we should not delay the query
                await QueryAsync(false, isReQuery: false);

                // set to false so the subsequent set true triggers
                // PropertyChanged and MoveQueryTextToEnd is called
                QueryTextCursorMovedToEnd = false;
            }
            else if (isReQuery)
            {
                // When we are re-querying, we should not delay the query
                await QueryAsync(false, isReQuery: true);
            }

            QueryTextCursorMovedToEnd = true;
        }

        public bool LastQuerySelected { get; set; }

        // This is not a reliable indicator of the cursor's position, it is manually set for a specific purpose.
        public bool QueryTextCursorMovedToEnd { get; set; }

        private ResultsViewModel _selectedResults;

        private ResultsViewModel SelectedResults
        {
            get => _selectedResults;
            set
            {
                var isReturningFromQueryResults = QueryResultsSelected();
                var isReturningFromContextMenu = ContextMenuSelected();
                var isReturningFromHistory = HistorySelected();
                _selectedResults = value;
                if (QueryResultsSelected())
                {
                    Results.Visibility = Visibility.Visible;
                    ContextMenu.Visibility = Visibility.Collapsed;
                    History.Visibility = Visibility.Collapsed;

                    // QueryText setter (used in ChangeQueryText) runs the query again, resetting the selected
                    // result from the one that was selected before going into the context menu to the first result.
                    // The code below correctly restores QueryText and puts the text caret at the end without
                    // running the query again when returning from the context menu.
                    if (isReturningFromContextMenu)
                    {
                        _queryText = _queryTextBeforeLeaveResults;
                        // When executing OnPropertyChanged, QueryTextBox_TextChanged1 and Query will be called
                        // So we need to ignore it so that we will not call Query again
                        _ignoredQueryText = _queryText;
                        OnPropertyChanged(nameof(QueryText));
                        QueryTextCursorMovedToEnd = true;
                    }
                    else
                    {
                        ChangeQueryText(_queryTextBeforeLeaveResults);
                    }

                    // If we are returning from history and we have not set select item yet,
                    // we need to clear the preview selected item
                    if (isReturningFromHistory && _selectedItemFromQueryResults.HasValue && (!_selectedItemFromQueryResults.Value))
                    {
                        PreviewSelectedItem = null;
                    }
                }
                else
                {
                    Results.Visibility = Visibility.Collapsed;
                    if (HistorySelected())
                    {
                        ContextMenu.Visibility = Visibility.Collapsed;
                        History.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ContextMenu.Visibility = Visibility.Visible;
                        History.Visibility = Visibility.Collapsed;
                    }
                    _queryTextBeforeLeaveResults = QueryText;

                    // Because of Fody's optimization
                    // setter won't be called when property value is not changed.
                    // so we need manually call Query()
                    // http://stackoverflow.com/posts/25895769/revisions
                    QueryText = string.Empty;
                    // When we are changing query because selected results are changed to history or context menu,
                    // we should not delay the query
                    Query(false);

                    if (HistorySelected())
                    {
                        // If we are returning from query results and we have not set select item yet,
                        // we need to clear the preview selected item
                        if (isReturningFromQueryResults && _selectedItemFromQueryResults.HasValue && _selectedItemFromQueryResults.Value)
                        {
                            PreviewSelectedItem = null;
                        }
                    }
                }
            }
        }

        public Visibility ShowCustomizedPreview
            => InternalPreviewVisible && PreviewSelectedItem?.Result.PreviewPanel != null ? Visibility.Visible : Visibility.Collapsed;

        public UserControl CustomizedPreviewControl
            => ShowCustomizedPreview == Visibility.Visible ? PreviewSelectedItem?.Result.PreviewPanel.Value : null;

        public Visibility ProgressBarVisibility { get; set; }
        public Visibility MainWindowVisibility { get; set; }

        // This is to be used for determining the visibility status of the main window instead of MainWindowVisibility
        // because it is more accurate and reliable representation than using Visibility as a condition check
        public bool MainWindowVisibilityStatus { get; set; } = true;

        public event VisibilityChangedEventHandler VisibilityChanged;
        public event ActualApplicationThemeChangedEventHandler ActualApplicationThemeChanged;

        public Visibility ClockPanelVisibility { get; set; }
        public Visibility SearchIconVisibility { get; set; }
        public double ClockPanelOpacity { get; set; } = 1;
        public double SearchIconOpacity { get; set; } = 1;

        private string _placeholderText;
        public string PlaceholderText
        {
            get => string.IsNullOrEmpty(_placeholderText) ? Localize.queryTextBoxPlaceholder() : _placeholderText;
            set
            {
                _placeholderText = value;
                OnPropertyChanged();
            }
        }

        public double MainWindowWidth
        {
            get => Settings.WindowSize;
            set
            {
                if (!MainWindowVisibilityStatus) return;
                Settings.WindowSize = value;
            }
        }

        public double MainWindowHeight
        {
            get => Settings.WindowHeightSize;
            set => Settings.WindowHeightSize = value;
        }

        public double QueryBoxFontSize
        {
            get => Settings.QueryBoxFontSize;
            set => Settings.QueryBoxFontSize = value;
        }

        public double ItemHeightSize
        {
            get => Settings.ItemHeightSize;
            set => Settings.ItemHeightSize = value;
        }

        public double ResultItemFontSize
        {
            get => Settings.ResultItemFontSize;
            set => Settings.ResultItemFontSize = value;
        }

        public double ResultSubItemFontSize
        {
            get => Settings.ResultSubItemFontSize;
            set => Settings.ResultSubItemFontSize = value;
        }

        public ImageSource PluginIconSource { get; private set; } = null;

        public string PluginIconPath { get; set; } = null;

        public string OpenResultCommandModifiers => Settings.OpenResultModifiers;

        private static string VerifyOrSetDefaultHotkey(string hotkey, string defaultHotkey)
        {
            try
            {
                var converter = new KeyGestureConverter();
                var key = (KeyGesture)converter.ConvertFromString(hotkey);
            }
            catch (Exception e) when (e is NotSupportedException || e is InvalidEnumArgumentException)
            {
                return defaultHotkey;
            }

            return hotkey;
        }

        public string PreviewHotkey => VerifyOrSetDefaultHotkey(Settings.PreviewHotkey, "F1");
        public string AutoCompleteHotkey => VerifyOrSetDefaultHotkey(Settings.AutoCompleteHotkey, "Ctrl+Tab");
        public string AutoCompleteHotkey2 => VerifyOrSetDefaultHotkey(Settings.AutoCompleteHotkey2, "");
        public string SelectNextItemHotkey => VerifyOrSetDefaultHotkey(Settings.SelectNextItemHotkey, "Tab");
        public string SelectNextItemHotkey2 => VerifyOrSetDefaultHotkey(Settings.SelectNextItemHotkey2, "");
        public string SelectPrevItemHotkey => VerifyOrSetDefaultHotkey(Settings.SelectPrevItemHotkey, "Shift+Tab");
        public string SelectPrevItemHotkey2 => VerifyOrSetDefaultHotkey(Settings.SelectPrevItemHotkey2, "");
        public string SelectNextPageHotkey => VerifyOrSetDefaultHotkey(Settings.SelectNextPageHotkey, "");
        public string SelectPrevPageHotkey => VerifyOrSetDefaultHotkey(Settings.SelectPrevPageHotkey, "");
        public string OpenContextMenuHotkey => VerifyOrSetDefaultHotkey(Settings.OpenContextMenuHotkey, "Ctrl+O");
        public string SettingWindowHotkey => VerifyOrSetDefaultHotkey(Settings.SettingWindowHotkey, "Ctrl+I");
        public string OpenHistoryHotkey => VerifyOrSetDefaultHotkey(Settings.OpenHistoryHotkey, "Ctrl+H");
        public string CycleHistoryUpHotkey => VerifyOrSetDefaultHotkey(Settings.CycleHistoryUpHotkey, "Alt+Up");
        public string CycleHistoryDownHotkey => VerifyOrSetDefaultHotkey(Settings.CycleHistoryDownHotkey, "Alt+Down");

        public bool StartWithEnglishMode => Settings.AlwaysStartEn;

        #endregion

        #region Preview

        private static readonly int ResultAreaColumnPreviewShown = 1;
        private static readonly int ResultAreaColumnPreviewHidden = 3;

        private bool? _selectedItemFromQueryResults;

        private ResultViewModel _previewSelectedItem;
        public ResultViewModel PreviewSelectedItem
        {
            get => _previewSelectedItem;
            set
            {
                _previewSelectedItem = value;
                OnPropertyChanged();
            }
        }

        public bool InternalPreviewVisible
        {
            get
            {
                if (ResultAreaColumn == ResultAreaColumnPreviewShown)
                    return true;

                if (ResultAreaColumn == ResultAreaColumnPreviewHidden)
                    return false;
#if DEBUG
                throw new NotImplementedException("ResultAreaColumn should match ResultAreaColumnPreviewShown/ResultAreaColumnPreviewHidden value");
#else
                App.API.LogError(ClassName, "ResultAreaColumnPreviewHidden/ResultAreaColumnPreviewShown int value not implemented", "InternalPreviewVisible");
                return false;
#endif
            }
        }

        public int ResultAreaColumn { get; set; } = ResultAreaColumnPreviewShown;

        // This is not a reliable indicator of whether external preview is visible due to the
        // ability of manually closing/exiting the external preview program which, does not inform flow that
        // preview is no longer available.
        public bool ExternalPreviewVisible { get; private set; }

        private async Task ShowPreviewAsync()
        {
            var useExternalPreview = PluginManager.UseExternalPreview();

            switch (useExternalPreview)
            {
                case true
                    when CanExternalPreviewSelectedResult(out var path):
                    // Internal preview may still be on when user switches to external
                    if (InternalPreviewVisible)
                        HideInternalPreview();

                    _ = OpenExternalPreviewAsync(path);
                    break;

                case true
                    when !CanExternalPreviewSelectedResult(out var _):
                    if (ExternalPreviewVisible)
                        await CloseExternalPreviewAsync();

                    ShowInternalPreview();
                    break;

                case false:
                    ShowInternalPreview();
                    break;
            }
        }

        private void HidePreview()
        {
            if (PluginManager.UseExternalPreview())
                _ = CloseExternalPreviewAsync();

            if (InternalPreviewVisible)
                HideInternalPreview();
        }

        [RelayCommand]
        private void TogglePreview()
        {
            if (InternalPreviewVisible || ExternalPreviewVisible)
            {
                HidePreview();
            }
            else
            {
                _ = ShowPreviewAsync();
            }
        }

        private async Task OpenExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await PluginManager.OpenExternalPreviewAsync(path, sendFailToast).ConfigureAwait(false);
            ExternalPreviewVisible = true;
        }

        private async Task CloseExternalPreviewAsync()
        {
            await PluginManager.CloseExternalPreviewAsync().ConfigureAwait(false);
            ExternalPreviewVisible = false;
        }

        private static async Task SwitchExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await PluginManager.SwitchExternalPreviewAsync(path, sendFailToast).ConfigureAwait(false);
        }

        private void ShowInternalPreview()
        {
            ResultAreaColumn = ResultAreaColumnPreviewShown;
            PreviewSelectedItem?.LoadPreviewImage();
        }

        private void HideInternalPreview()
        {
            ResultAreaColumn = ResultAreaColumnPreviewHidden;
        }

        public void ResetPreview()
        {
            switch (Settings.AlwaysPreview)
            {
                case true
                    when PluginManager.AllowAlwaysPreview() && CanExternalPreviewSelectedResult(out var path):
                    _ = OpenExternalPreviewAsync(path);
                    break;
                case true:
                    ShowInternalPreview();
                    break;
                case false:
                    HidePreview();
                    break;
            }
        }

        private async Task UpdatePreviewAsync()
        {
            switch (PluginManager.UseExternalPreview())
            {
                case true
                    when CanExternalPreviewSelectedResult(out var path):
                    if (ExternalPreviewVisible)
                    {
                        _ = SwitchExternalPreviewAsync(path, false);
                    }
                    else if (InternalPreviewVisible)
                    {
                        HideInternalPreview();
                        _ = OpenExternalPreviewAsync(path);
                    }

                    break;
                case true
                    when !CanExternalPreviewSelectedResult(out var _):
                    if (ExternalPreviewVisible)
                    {
                        await CloseExternalPreviewAsync();
                        ShowInternalPreview();
                    }
                    break;
                case false
                    when InternalPreviewVisible:
                    PreviewSelectedItem?.LoadPreviewImage();
                    break;
            }
        }

        private bool CanExternalPreviewSelectedResult(out string path)
        {
            path = QueryResultsPreviewed() ? Results.SelectedItem?.Result?.Preview.FilePath : string.Empty;
            return !string.IsNullOrEmpty(path);
        }

        private bool QueryResultsPreviewed()
        {
            var previewed = PreviewSelectedItem == Results.SelectedItem;
            return previewed;
        }

        #endregion

        #region Query

        public void QueryResults()
        {
            _ = QueryResultsAsync(false);
        }

        public void Query(bool searchDelay, bool isReQuery = false)
        {
            if (_ignoredQueryText != null)
            {
                if (_ignoredQueryText == QueryText)
                {
                    _ignoredQueryText = null;
                    return;
                }
                else
                {
                    // If _ignoredQueryText does not match current QueryText, we should still execute Query
                    _ignoredQueryText = null;
                }
            }

            if (QueryResultsSelected())
            {
                _ = QueryResultsAsync(searchDelay, isReQuery);
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

        private async Task QueryAsync(bool searchDelay, bool isReQuery = false)
        {
            if (QueryResultsSelected())
            {
                await QueryResultsAsync(searchDelay, isReQuery);
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
                List<Result> results;
                if (selected.PluginID == null) // SelectedItem from history in home page.
                {
                    results = new()
                    {
                        ContextMenuTopMost(selected)
                    };
                }
                else
                {
                    results = PluginManager.GetContextMenusForPlugin(selected);
                    results.Add(ContextMenuTopMost(selected));
                    results.Add(ContextMenuPluginInfo(selected));
                }

                if (!string.IsNullOrEmpty(query))
                {
                    var filtered = results.Select(x => x.Clone()).Where
                    (
                        r =>
                       {
                           var match = App.API.FuzzySearch(query, r.Title);
                           if (!match.IsSearchPrecisionScoreMet())
                           {
                               match = App.API.FuzzySearch(query, r.SubTitle);
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

            var results = GetHistoryItems(_history.LastOpenedHistoryItems);

            if (!string.IsNullOrEmpty(query))
            {
                var filtered = results.Where
                (
                    r => App.API.FuzzySearch(query, r.Title).IsSearchPrecisionScoreMet() ||
                         App.API.FuzzySearch(query, r.SubTitle).IsSearchPrecisionScoreMet()
                ).ToList();
                History.AddResults(filtered, id);
            }
            else
            {
                History.AddResults(results, id);
            }
        }

        private List<Result> GetHistoryItems(IEnumerable<LastOpenedHistoryItem> historyItems)
        {
            var results = new List<Result>();
            if (Settings.HistoryStyle == HistoryStyle.Query)
            {
                foreach (var h in historyItems)
                {
                    var result = new Result
                    {
                        Title = Localize.executeQuery(h.Query),
                        SubTitle = Localize.lastExecuteTime(h.ExecutedDateTime),
                        IcoPath = Constant.HistoryIcon,
                        OriginQuery = new Query { RawQuery = h.Query },
                        Action = _ =>
                        {
                            App.API.BackToQueryResults();
                            App.API.ChangeQuery(h.Query);
                            return false;
                        },
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE81C")
                    };
                    results.Add(result);
                }
            }
            else
            {
                foreach (var h in historyItems)
                {
                    var result = new Result
                    {
                        Title = string.IsNullOrEmpty(h.Title) ?  // Old migrated history items have no title
                            Localize.executeQuery(h.Query) :
                            h.Title,
                        SubTitle = Localize.lastExecuteTime(h.ExecutedDateTime),
                        IcoPath = Constant.HistoryIcon,
                        OriginQuery = new Query { RawQuery = h.Query },
                        AsyncAction = async c =>
                        {
                            var reflectResult = await ResultHelper.PopulateResultsAsync(h);
                            if (reflectResult != null)
                            {
                                // Record the user selected record for result ranking
                                _userSelectedRecord.Add(reflectResult);

                                // Since some actions may need to hide the Flow window to execute
                                // So let us populate the results of them
                                return await reflectResult.ExecuteAsync(c);
                            }

                            // If we cannot get the result, fallback to re-query
                            App.API.BackToQueryResults();
                            App.API.ChangeQuery(h.Query);
                            return false;
                        },
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE81C")
                    };
                    results.Add(result);
                }
            }
            return results;
        }

        private async Task QueryResultsAsync(bool searchDelay, bool isReQuery = false, bool reSelect = true)
        {
            _updateSource?.Cancel();

            App.API.LogDebug(ClassName, $"Start query with text: <{QueryText}>");

            var query = await ConstructQueryAsync(QueryText, Settings.CustomShortcuts, Settings.BuiltinShortcuts);

            if (query == null) // shortcut expanded
            {
                ClearResults();
                return;
            }

            App.API.LogDebug(ClassName, $"Start query with ActionKeyword <{query.ActionKeyword}> and RawQuery <{query.RawQuery}>");

            var currentIsHomeQuery = query.IsHomeQuery;
            var currentIsDialogJump = _isDialogJump;

            // Do not show home page for Dialog Jump window
            if (currentIsHomeQuery && currentIsDialogJump)
            {
                ClearResults();
                return;
            }

            try
            {
                _updateSource?.Dispose();

                var currentUpdateSource = new CancellationTokenSource();
                _updateSource = currentUpdateSource;
                var currentCancellationToken = _updateSource.Token;
                _updateToken = currentCancellationToken;

                ProgressBarVisibility = Visibility.Hidden;

                _progressQuery = query;
                _updateQuery = query;

                // Switch to ThreadPool thread
                await TaskScheduler.Default;

                if (currentCancellationToken.IsCancellationRequested) return;

                // Update the query's IsReQuery property to true if this is a re-query
                query.IsReQuery = isReQuery;

                ICollection<PluginPair> plugins = Array.Empty<PluginPair>();
                if (currentIsHomeQuery)
                {
                    if (Settings.ShowHomePage)
                    {
                        plugins = PluginManager.ValidPluginsForHomeQuery();
                    }

                    PluginIconPath = null;
                    PluginIconSource = null;
                    SearchIconVisibility = Visibility.Visible;
                }
                else
                {
                    plugins = PluginManager.ValidPluginsForQuery(query, currentIsDialogJump);

                    if (plugins.Count == 1)
                    {
                        PluginIconPath = plugins.Single().Metadata.IcoPath;
                        PluginIconSource = await App.API.LoadImageAsync(PluginIconPath);
                        SearchIconVisibility = Visibility.Hidden;
                    }
                    else
                    {
                        PluginIconPath = null;
                        PluginIconSource = null;
                        SearchIconVisibility = Visibility.Visible;
                    }
                }

                App.API.LogDebug(ClassName, $"Valid <{plugins.Count}> plugins: {string.Join(" ", plugins.Select(x => $"<{x.Metadata.Name}>"))}");

                // Do not wait for performance improvement
                /*if (string.IsNullOrEmpty(query.ActionKeyword))
                {
                    // Wait 15 millisecond for query change in global query
                    // if query changes, return so that it won't be calculated
                    await Task.Delay(15, currentCancellationToken);
                    if (currentCancellationToken.IsCancellationRequested) return;
                }*/

                _ = Task.Delay(200, currentCancellationToken).ContinueWith(_ =>
                {
                    // start the progress bar if query takes more than 200 ms and this is the current running query and it didn't finish yet
                    if (_progressQuery != null && _progressQuery.Input == query.Input)
                    {
                        ProgressBarVisibility = Visibility.Visible;
                    }
                },
                currentCancellationToken,
                TaskContinuationOptions.NotOnCanceled,
                TaskScheduler.Default);

                // plugins are ICollection, meaning LINQ will get the Count and preallocate Array

                Task[] tasks;
                if (currentIsHomeQuery)
                {
                    if (ShouldClearExistingResultsForNonQuery(plugins))
                    {
                        // there are no update tasks and so we can directly return
                        ClearResults();
                        return;
                    }

                    tasks = plugins.Select(plugin => plugin.Metadata.HomeDisabled switch
                    {
                        false => QueryTaskAsync(plugin, currentCancellationToken),
                        true => Task.CompletedTask
                    }).ToArray();

                    // Query history results for home page firstly so it will be put on top of the results
                    if (Settings.ShowHistoryResultsForHomePage)
                    {
                        QueryHistoryTask(currentCancellationToken);
                    }
                }
                else
                {
                    tasks = plugins.Select(plugin => plugin.Metadata.Disabled switch
                    {
                        false => QueryTaskAsync(plugin, currentCancellationToken),
                        true => Task.CompletedTask
                    }).ToArray();
                }

                try
                {
                    // Check the code, WhenAll will translate all type of IEnumerable or Collection to Array, so make an array at first
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    // nothing to do here
                }

                if (currentCancellationToken.IsCancellationRequested) return;

                // this should happen once after all queries are done so progress bar should continue
                // until the end of all querying
                _progressQuery = null;

                if (!currentCancellationToken.IsCancellationRequested)
                {
                    // update to hidden if this is still the current query
                    ProgressBarVisibility = Visibility.Hidden;
                }
            }
            finally
            {
                // this make sures progress query is null when this query is canceled
                _progressQuery = null;
            }

            // Local function
            void ClearResults()
            {
                App.API.LogDebug(ClassName, $"Clear query results");

                // Hide and clear results again because running query may show and add some results
                Results.Visibility = Visibility.Collapsed;
                Results.Clear();

                // Reset plugin icon
                PluginIconPath = null;
                PluginIconSource = null;
                SearchIconVisibility = Visibility.Visible;

                // Hide progress bar again because running query may set this to visible
                ProgressBarVisibility = Visibility.Hidden;
            }

            async Task QueryTaskAsync(PluginPair plugin, CancellationToken token)
            {
                App.API.LogDebug(ClassName, $"Wait for querying plugin <{plugin.Metadata.Name}>");

                if (searchDelay && !currentIsHomeQuery) // Do not delay for home query
                {
                    var searchDelayTime = plugin.Metadata.SearchDelayTime ?? Settings.SearchDelayTime;

                    await Task.Delay(searchDelayTime, token);

                    if (token.IsCancellationRequested) return;
                }

                // Since it is wrapped within a ThreadPool Thread, the synchronous context is null
                // Task.Yield will force it to run in ThreadPool
                await Task.Yield();

                IReadOnlyList<Result> results = currentIsDialogJump ?
                    await PluginManager.QueryDialogJumpForPluginAsync(plugin, query, token) :
                        currentIsHomeQuery ?
                            await PluginManager.QueryHomeForPluginAsync(plugin, query, token) :
                            await PluginManager.QueryForPluginAsync(plugin, query, token);

                if (token.IsCancellationRequested) return;

                IReadOnlyList<Result> resultsCopy;
                if (results == null)
                {
                    resultsCopy = currentIsDialogJump ? _emptyDialogJumpResult : _emptyResult;
                }
                else
                {
                    // make a copy of results to avoid possible issue that FL changes some properties of the records, like score, etc.
                    resultsCopy = DeepCloneResults(results, currentIsDialogJump, token);
                }

                foreach (var result in resultsCopy)
                {
                    if (string.IsNullOrEmpty(result.BadgeIcoPath))
                    {
                        result.BadgeIcoPath = plugin.Metadata.IcoPath;
                    }
                }

                if (token.IsCancellationRequested) return;

                App.API.LogDebug(ClassName, $"Update results for plugin <{plugin.Metadata.Name}>");

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(resultsCopy, plugin.Metadata, query,
                    token, reSelect)))
                {
                    App.API.LogError(ClassName, "Unable to add item to Result Update Queue");
                }
            }

            void QueryHistoryTask(CancellationToken token)
            {
                // Select last history results and revert its order to make sure last history results are on top
                var historyItems = _history.LastOpenedHistoryItems.TakeLast(Settings.MaxHistoryResultsToShowForHomePage).Reverse();

                var results = GetHistoryItems(historyItems);

                if (token.IsCancellationRequested) return;

                App.API.LogDebug(ClassName, $"Update results for history");

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(results, _historyMetadata, query,
                    token, reSelect)))
                {
                    App.API.LogError(ClassName, "Unable to add item to Result Update Queue");
                }
            }
        }

        private async Task<Query> ConstructQueryAsync(string queryText, IEnumerable<CustomShortcutModel> customShortcuts,
            IEnumerable<BaseBuiltinShortcutModel> builtInShortcuts)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return QueryBuilder.Build(string.Empty, string.Empty, PluginManager.GetNonGlobalPlugins());
            }

            var queryBuilder = new StringBuilder(queryText);
            var queryBuilderTmp = new StringBuilder(queryText);

            // Sorting order is important here, the reason is for matching longest shortcut by default
            foreach (var shortcut in customShortcuts.OrderByDescending(x => x.Key.Length))
            {
                if (queryBuilder.Equals(shortcut.Key))
                {
                    queryBuilder.Replace(shortcut.Key, shortcut.Expand());
                }

                queryBuilder.Replace('@' + shortcut.Key, shortcut.Expand());
            }

            // Applying builtin shortcuts
            await BuildQueryAsync(builtInShortcuts, queryBuilder, queryBuilderTmp);

            return QueryBuilder.Build(queryText, queryBuilder.ToString().Trim(), PluginManager.GetNonGlobalPlugins());
        }

        private async Task BuildQueryAsync(IEnumerable<BaseBuiltinShortcutModel> builtInShortcuts,
            StringBuilder queryBuilder, StringBuilder queryBuilderTmp)
        {
            var customExpanded = queryBuilder.ToString();

            var queryChanged = false;

            foreach (var shortcut in builtInShortcuts)
            {
                try
                {
                    if (customExpanded.Contains(shortcut.Key))
                    {
                        string expansion;
                        if (shortcut is BuiltinShortcutModel syncShortcut)
                        {
                            expansion = syncShortcut.Expand();
                        }
                        else if (shortcut is AsyncBuiltinShortcutModel asyncShortcut)
                        {
                            expansion = await asyncShortcut.ExpandAsync();
                        }
                        else
                        {
                            continue;
                        }
                        queryBuilder.Replace(shortcut.Key, expansion);
                        queryBuilderTmp.Replace(shortcut.Key, expansion);
                        queryChanged = true;
                    }
                }
                catch (Exception e)
                {
                    App.API.LogException(ClassName, $"Error when expanding shortcut {shortcut.Key}", e);
                }
            }

            // Show expanded builtin shortcuts
            if (queryChanged)
            {
                // Use private field to avoid infinite recursion
                _queryText = queryBuilderTmp.ToString();
                // When executing OnPropertyChanged, QueryTextBox_TextChanged1 and Query will be called
                // So we need to ignore it so that we will not call Query again
                _ignoredQueryText = _queryText;
                OnPropertyChanged(nameof(QueryText));
            }
        }

        /// <summary>
        /// Determines whether the existing search results should be cleared based on the current query and the previous query type.
        /// This is used to indicate to QueryTaskAsync or QueryHistoryTask whether to clear results. If both QueryTaskAsync and QueryHistoryTask
        /// are not called then use ShouldClearExistingResultsForNonQuery instead.
        /// This method needed because of the design that treats plugins with action keywords and global action keywords separately. Results are gathered
        /// either from plugins with matching action keywords or global action keyword, but not both. So when the current results are from plugins
        /// with a matching action keyword and a new result set comes from a new query with the global action keyword, the existing results need to be cleared,
        /// and vice versa. The same applies to home page query results.
        /// 
        /// There is no need to clear results from global action keyword if a new set of results comes along that is also from global action keywords.
        /// This is because the removal of obsolete results is handled in ResultsViewModel.NewResults(ICollection<ResultsForUpdate>).
        /// </summary>
        /// <param name="query">The current query.</param>
        /// <param name="currentIsHomeQuery">A flag indicating if the current query is a home query.</param>
        /// <returns>True if the existing results should be cleared, false otherwise.</returns>
        private bool ShouldClearExistingResultsForQuery(Query query, bool currentIsHomeQuery)
        {
            // If previous or current results are from home query, we need to clear them
            if (_previousIsHomeQuery || currentIsHomeQuery)
            {
                App.API.LogDebug(ClassName, $"Existing results should be cleared for query");
                return true;
            }

            // If the last and current query are not home query type, we need to check the action keyword
            if (_lastQuery?.ActionKeyword != query?.ActionKeyword)
            {
                App.API.LogDebug(ClassName, $"Existing results should be cleared for query");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether existing results should be cleared for non-query calls.
        /// A non-query call is where QueryTaskAsync and QueryHistoryTask methods are both not called.
        /// QueryTaskAsync and QueryHistoryTask both handle result updating (clearing if required) so directly calling
        /// Results.Clear() is not required. However when both are not called, we need to directly clear results and this
        /// method determines on the condition when clear results should happen.
        /// </summary>
        /// <param name="plugins">The collection of plugins to check.</param>
        /// <returns>True if existing results should be cleared, false otherwise.</returns>
        private bool ShouldClearExistingResultsForNonQuery(ICollection<PluginPair> plugins)
        {
            if (!Settings.ShowHistoryResultsForHomePage && (plugins.Count == 0 || plugins.All(x => x.Metadata.HomeDisabled == true)))
            {
                App.API.LogDebug(ClassName, $"Existing results should be cleared for non-query");
                return true;
            }

            return false;
        }

        private Result ContextMenuTopMost(Result result)
        {
            Result menu;
            if (_topMostRecord.IsTopMost(result))
            {
                menu = new Result
                {
                    Title = Localize.cancelTopMostInThisQuery(),
                    IcoPath = "Images\\down.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.Remove(result);
                        App.API.ShowMsg(Localize.success());
                        App.API.ReQuery();
                        return false;
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE74B"),
                    OriginQuery = result.OriginQuery
                };
            }
            else
            {
                menu = new Result
                {
                    Title = Localize.setAsTopMostInThisQuery(),
                    IcoPath = "Images\\up.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.AddOrUpdate(result);
                        App.API.ShowMsg(Localize.success());
                        App.API.ReQuery();
                        return false;
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE74A"),
                    OriginQuery = result.OriginQuery
                };
            }

            return menu;
        }

        private static Result ContextMenuPluginInfo(Result result)
        {
            var id = result.PluginID;
            var metadata = PluginManager.GetPluginForId(id).Metadata;
            var translator = App.API;

            var author = Localize.author();
            var website = Localize.website();
            var version = Localize.version();
            var plugin = Localize.plugin();
            var title = $"{plugin}: {metadata.Name}";
            var icon = metadata.IcoPath;
            var subtitle = $"{author} {metadata.Author}";

            var menu = new Result
            {
                Title = title,
                IcoPath = icon,
                SubTitle = subtitle,
                PluginDirectory = metadata.PluginDirectory,
                Action = _ =>
                {
                    App.API.OpenUrl(metadata.Website);
                    return true;
                },
                OriginQuery = result.OriginQuery
            };
            return menu;
        }

        internal bool QueryResultsSelected()
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

        internal bool ResultsSelected(ResultsViewModel results)
        {
            var selected = SelectedResults == results;
            return selected;
        }

        #endregion

        #region Hotkey

        public void ToggleFlowLauncher()
        {
            if (!MainWindowVisibilityStatus)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        /// <summary>
        /// Checks if Flow Launcher should ignore any hotkeys
        /// </summary>
        public bool ShouldIgnoreHotkeys()
        {
            return Settings.IgnoreHotkeysOnFullscreen && Win32Helper.IsForegroundWindowFullscreen() || GameModeStatus;
        }

        #endregion

        #region Dialog Jump

        public nint DialogWindowHandle { get; private set; } = nint.Zero;

        private bool _isDialogJump = false;

        private bool _previousMainWindowVisibilityStatus;

        private CancellationTokenSource _dialogJumpSource;

        public void InitializeVisibilityStatus(bool visibilityStatus)
        {
            _previousMainWindowVisibilityStatus = visibilityStatus;
        }

        public bool IsDialogJumpWindowUnderDialog()
        {
            return _isDialogJump && DialogJump.DialogJumpWindowPosition == DialogJumpWindowPositions.UnderDialog;
        }

        public async Task SetupDialogJumpAsync(nint handle)
        {
            if (handle == nint.Zero) return;

            // Only set flag & reset window once for one file dialog
            var dialogWindowHandleChanged = false;
            if (DialogWindowHandle != handle)
            {
                DialogWindowHandle = handle;
                _previousMainWindowVisibilityStatus = MainWindowVisibilityStatus;
                _isDialogJump = true;

                dialogWindowHandleChanged = true;

                // If don't give a time, Positioning will be weird
                await Task.Delay(300);
            }

            // If handle is cleared, which means the dialog is closed, clear Dialog Jump state
            if (DialogWindowHandle == nint.Zero)
            {
                _isDialogJump = false;
                return;
            }

            // Initialize Dialog Jump window
            if (MainWindowVisibilityStatus)
            {
                if (dialogWindowHandleChanged)
                {
                    // Only update the position
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        (Application.Current?.MainWindow as MainWindow)?.UpdatePosition();
                    });

                    _ = ResetWindowAsync();
                }
            }
            else
            {
                if (DialogJump.DialogJumpWindowPosition == DialogJumpWindowPositions.UnderDialog)
                {
                    // We wait for window to be reset before showing it because if window has results,
                    // showing it before resetting will cause flickering when results are clearing
                    if (dialogWindowHandleChanged)
                    {
                        await ResetWindowAsync();
                    }

                    Show();
                }
                else
                {
                    if (dialogWindowHandleChanged)
                    {
                        _ = ResetWindowAsync();
                    }
                }
            }

            if (DialogJump.DialogJumpWindowPosition == DialogJumpWindowPositions.UnderDialog)
            {
                // Cancel the previous Dialog Jump task
                _dialogJumpSource?.Cancel();

                // Create a new cancellation token source
                _dialogJumpSource = new CancellationTokenSource();

                _ = Task.Run(() =>
                {
                    try
                    {
                        // Check task cancellation
                        if (_dialogJumpSource.Token.IsCancellationRequested) return;

                        // Check dialog handle
                        if (DialogWindowHandle == nint.Zero) return;

                        // Wait 150ms to check if Dialog Jump window gets the focus
                        var timeOut = !SpinWait.SpinUntil(() => !Win32Helper.IsForegroundWindow(DialogWindowHandle), 150);
                        if (timeOut) return;

                        // Bring focus back to the dialog
                        Win32Helper.SetForegroundWindow(DialogWindowHandle);
                    }
                    catch (Exception e)
                    {
                        App.API.LogException(ClassName, "Failed to focus on dialog window", e);
                    }
                });
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods

        public async void ResetDialogJump()
        {
            // Cache original dialog window handle
            var dialogWindowHandle = DialogWindowHandle;

            // Reset the Dialog Jump state
            DialogWindowHandle = nint.Zero;
            _isDialogJump = false;

            // If dialog window handle is not set, we should not reset the main window visibility
            if (dialogWindowHandle == nint.Zero) return;

            if (_previousMainWindowVisibilityStatus != MainWindowVisibilityStatus)
            {
                // We wait for window to be reset before showing it because if window has results,
                // showing it before resetting will cause flickering when results are clearing
                await ResetWindowAsync();

                // Show or hide to change visibility
                if (_previousMainWindowVisibilityStatus)
                {
                    Show();
                }
                else
                {
                    Hide(false);
                }
            }
            else
            {
                if (_previousMainWindowVisibilityStatus)
                {
                    // Only update the position
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        (Application.Current?.MainWindow as MainWindow)?.UpdatePosition();
                    });

                    _ = ResetWindowAsync();
                }
                else
                {
                    _ = ResetWindowAsync();
                }
            }
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        public void HideDialogJump()
        {
            if (DialogWindowHandle != nint.Zero)
            {
                if (DialogJump.DialogJumpWindowPosition == DialogJumpWindowPositions.UnderDialog)
                {
                    // Warning: Main window is already in foreground
                    // This is because if you click popup menus in other applications to hide Dialog Jump window,
                    // they can steal focus before showing main window
                    if (MainWindowVisibilityStatus)
                    {
                        Hide();
                    }
                }
            }
        }

        // Reset index & preview & selected results & query text
        private async Task ResetWindowAsync()
        {
            lastHistoryIndex = 1;

            if (ExternalPreviewVisible)
            {
                await CloseExternalPreviewAsync();
            }

            if (!QueryResultsSelected())
            {
                SelectedResults = Results;
            }

            await ChangeQueryTextAsync(string.Empty, true);
        }

        #endregion

        #region Public Methods

#pragma warning disable VSTHRD100 // Avoid async void methods

        public void Show()
        {
            // When application is exiting, we should not show the main window
            if (App.LoadingOrExiting) return;

            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    // 📌 Remove DWM Cloak (Make the window visible normally)
                    Win32Helper.DWMSetCloakForWindow(mainWindow, false);

                    // Set clock and search icon opacity
                    var opacity = (Settings.UseAnimation && !_isDialogJump) ? 0.0 : 1.0;
                    ClockPanelOpacity = opacity;
                    SearchIconOpacity = opacity;

                    // Set clock and search icon visibility
                    ClockPanelVisibility = string.IsNullOrEmpty(QueryText) ? Visibility.Visible : Visibility.Collapsed;
                    if (PluginIconSource != null)
                    {
                        SearchIconOpacity = 0.0;
                    }
                    else
                    {
                        SearchIconVisibility = Visibility.Visible;
                    }
                }
            }, DispatcherPriority.Render);

            // Update WPF properties
            MainWindowVisibility = Visibility.Visible;
            MainWindowVisibilityStatus = true;
            VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs { IsVisible = true });

            // Switch keyboard layout
            if (StartWithEnglishMode)
            {
                Win32Helper.SwitchToEnglishKeyboardLayout(true);
            }
        }

        public async void Hide(bool reset = true)
        {
            if (reset)
            {
                lastHistoryIndex = 1;

                if (ExternalPreviewVisible)
                {
                    await CloseExternalPreviewAsync();
                }

                BackToQueryResults();

                switch (Settings.LastQueryMode)
                {
                    case LastQueryMode.Empty:
                        await ChangeQueryTextAsync(string.Empty);
                        break;
                    case LastQueryMode.Preserved:
                    case LastQueryMode.Selected:
                        LastQuerySelected = Settings.LastQueryMode == LastQueryMode.Preserved;
                        break;
                    case LastQueryMode.ActionKeywordPreserved:
                    case LastQueryMode.ActionKeywordSelected:
                        var newQuery = _lastQuery.ActionKeyword;

                        if (!string.IsNullOrEmpty(newQuery))
                            newQuery += " ";
                        await ChangeQueryTextAsync(newQuery);

                        if (Settings.LastQueryMode == LastQueryMode.ActionKeywordSelected)
                            LastQuerySelected = false;
                        break;
                }
            }

            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    // Set clock and search icon opacity
                    var opacity = (Settings.UseAnimation && !_isDialogJump) ? 0.0 : 1.0;
                    ClockPanelOpacity = opacity;
                    SearchIconOpacity = opacity;

                    // Set clock and search icon visibility
                    ClockPanelVisibility = Visibility.Hidden;
                    SearchIconVisibility = Visibility.Hidden;

                    // Force UI update
                    mainWindow.ClockPanel.UpdateLayout();
                    mainWindow.SearchIcon.UpdateLayout();

                    // 📌 Apply DWM Cloak (Completely hide the window)
                    Win32Helper.DWMSetCloakForWindow(mainWindow, true);
                }
            }, DispatcherPriority.Render);

            // Switch keyboard layout
            if (StartWithEnglishMode)
            {
                Win32Helper.RestorePreviousKeyboardLayout();
            }

            // Delay for a while to make sure clock will not flicker
            await Task.Delay(50);

            // Update WPF properties
            MainWindowVisibilityStatus = false;
            MainWindowVisibility = Visibility.Collapsed;
            VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs { IsVisible = false });
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        /// <summary>
        /// Save history, user selected records and top most records
        /// </summary>
        public void Save()
        {
            _historyItemsStorage.Save();
            _userSelectedRecordStorage.Save();
            _topMostRecord.Save();
        }

        /// <summary>
        /// To avoid deadlock, this method should not be called from main thread
        /// </summary>
        public void UpdateResultView(ICollection<ResultsForUpdate> resultsForUpdates)
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
                    var deviationIndex = _topMostRecord.GetTopMostIndex(result);
                    if (deviationIndex != -1)
                    {
                        // Adjust the score based on the result's position in the top-most list.
                        // A lower deviationIndex (closer to the top) results in a higher score.
                        result.Score = Result.MaxScore - deviationIndex;
                    }
                    else
                    {
                        var priorityScore = metaResults.Metadata.Priority * 150;
                        if (result.AddSelectedCount)
                        {
                            if ((long)result.Score + _userSelectedRecord.GetSelectedCount(result) + priorityScore > Result.MaxScore)
                            {
                                result.Score = Result.MaxScore;
                            }
                            else
                            {
                                result.Score += _userSelectedRecord.GetSelectedCount(result) + priorityScore;
                            }
                        }
                        else
                        {
                            if ((long)result.Score + priorityScore > Result.MaxScore)
                            {
                                result.Score = Result.MaxScore;
                            }
                            else
                            {
                                result.Score += priorityScore;
                            }
                        }
                    }
                }
            }

            // it should be the same for all results
            bool reSelect = resultsForUpdates.First().ReSelectFirstResult;

            Results.AddResults(resultsForUpdates, token, reSelect);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        public void FocusQueryTextBox()
        {
            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow window)
                {
                    window.QueryTextBox.Focus();
                    Keyboard.Focus(window.QueryTextBox);
                }
            });
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _updateSource?.Dispose();
                    _dialogJumpSource?.Dispose();
                    _resultsUpdateChannelWriter?.Complete();
                    if (_resultsViewUpdateTask?.IsCompleted == true)
                    {
                        _resultsViewUpdateTask.Dispose();
                    }
                    ThemeManager.Current.ActualApplicationThemeChanged -= ThemeManager_ActualApplicationThemeChanged;
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
