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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.QuickSwitch;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Storage;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.ViewModel
{
    public partial class MainViewModel : BaseModel, ISavable, IDisposable
    {
        #region Private Fields

        private static readonly string ClassName = nameof(MainViewModel);

        private bool _isQueryRunning;
        private Query _lastQuery;
        private string _queryTextBeforeLeaveResults;

        private readonly FlowLauncherJsonStorage<History> _historyItemsStorage;
        private readonly FlowLauncherJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly FlowLauncherJsonStorage<TopMostRecord> _topMostRecordStorage;
        private readonly History _history;
        private int lastHistoryIndex = 1;
        private readonly UserSelectedRecord _userSelectedRecord;
        private readonly TopMostRecord _topMostRecord;

        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;

        private ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
        private Task _resultsViewUpdateTask;

        private readonly IReadOnlyList<Result> _emptyResult = new List<Result>();

        #endregion

        #region Constructor

        public MainViewModel()
        {
            _queryTextBeforeLeaveResults = "";
            _queryText = "";
            _lastQuery = new Query();

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
                }
            };

            _historyItemsStorage = new FlowLauncherJsonStorage<History>();
            _userSelectedRecordStorage = new FlowLauncherJsonStorage<UserSelectedRecord>();
            _topMostRecordStorage = new FlowLauncherJsonStorage<TopMostRecord>();
            _history = _historyItemsStorage.Load();
            _userSelectedRecord = _userSelectedRecordStorage.Load();
            _topMostRecord = _topMostRecordStorage.Load();

            ContextMenu = new ResultsViewModel(Settings)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            Results = new ResultsViewModel(Settings)
            {
                LeftClickResultCommand = OpenResultCommand,
                RightClickResultCommand = LoadContextMenuCommand,
                IsPreviewOn = Settings.AlwaysPreview
            };
            History = new ResultsViewModel(Settings)
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
        }

        private void RegisterViewUpdate()
        {
            var resultUpdateChannel = Channel.CreateUnbounded<ResultsForUpdate>();
            _resultsUpdateChannelWriter = resultUpdateChannel.Writer;
            _resultsViewUpdateTask =
                Task.Run(UpdateActionAsync).ContinueWith(continueAction, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

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
                            queue[item.ID] = item;
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

        public void RegisterResultsUpdatedEvent()
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

                    // make a clone to avoid possible issue that plugin will also change the list and items when updating view model
                    var resultsCopy = CheckQuickSwitchAndDeepClone(e.Results, token);

                    foreach (var result in resultsCopy)
                    {
                        if (string.IsNullOrEmpty(result.BadgeIcoPath))
                        {
                            result.BadgeIcoPath = pair.Metadata.IcoPath;
                        }
                    }

                    PluginManager.UpdatePluginMetadata(resultsCopy, pair.Metadata, e.Query);
                    if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(resultsCopy, pair.Metadata, e.Query,
                            token)))
                    {
                        App.API.LogError(ClassName, "Unable to add item to Result Update Queue");
                    }
                };
            }
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
            App.API.ShowMsg(App.API.GetTranslation("success"),
                App.API.GetTranslation("completedSuccessfully"));
        }

        [RelayCommand]
        private void LoadHistory()
        {
            if (QueryResultsSelected())
            {
                SelectedResults = History;
                History.SelectedIndex = _history.Items.Count - 1;
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
            if (_history.Items.Count > 0)
            {
                ChangeQueryText(_history.Items[^lastHistoryIndex].Query);
                if (lastHistoryIndex < _history.Items.Count)
                {
                    lastHistoryIndex++;
                }
            }
        }

        [RelayCommand]
        public void ForwardHistory()
        {
            if (_history.Items.Count > 0)
            {
                ChangeQueryText(_history.Items[^lastHistoryIndex].Query);
                if (lastHistoryIndex > 1)
                {
                    lastHistoryIndex--;
                }
            }
        }

        [RelayCommand]
        private void LoadContextMenu()
        {
            if (QueryResultsSelected())
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
        }

        [RelayCommand]
        private void Backspace(object index)
        {
            var query = QueryBuilder.Build(QueryText.Trim(), PluginManager.NonGlobalPlugins);

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

            if (IsQuickSwitch)
            {
                Win32Helper.SetForegroundWindow(DialogWindowHandle);
                QuickSwitch.JumpToPath(result.QuickSwitchPath);
            }
            else
            {
                var hideWindow = await result.ExecuteAsync(new ActionContext
                {
                    // not null means pressing modifier key + number, should ignore the modifier key
                    SpecialKeyState = index is not null ? SpecialKeyState.Default : GlobalHotkey.CheckModifiers()
                })
                .ConfigureAwait(false);

                if (hideWindow)
                {
                    Hide();
                }
            }

            if (QueryResultsSelected())
            {
                _userSelectedRecord.Add(result);
                // origin query is null when user select the context menu item directly of one item from query list
                // so we don't want to add it to history
                if (result.OriginQuery != null)
                {
                    _history.Add(result.OriginQuery.RawQuery);
                }
                lastHistoryIndex = 1;
            }
        }

        private IReadOnlyList<Result> CheckQuickSwitchAndDeepClone(IReadOnlyList<Result> results, CancellationToken token = default)
        {
            var resultsCopy = new List<Result>();

            foreach (var result in results.ToList())
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (IsQuickSwitch && !result.AllowQuickSwitch)
                {
                    continue;
                }

                var resultCopy = result.Clone();
                resultsCopy.Add(resultCopy);
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
            if (_history.Items.Count > 0
                && QueryText == string.Empty
                && QueryResultsSelected())
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
            _ = ChangeQueryTextAsync(queryText, isReQuery);
        }

        /// <summary>
        /// Async version of <see cref="ChangeQueryText"/>
        /// </summary>
        private async Task ChangeQueryTextAsync(string queryText, bool isReQuery = false)
        {
            // Must check access so that we will not block the UI thread which cause window visibility issue
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(() => ChangeQueryText(queryText, isReQuery));
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

                _selectedResults.Visibility = Visibility.Visible;
            }
        }

        public Visibility ProgressBarVisibility { get; set; }
        public Visibility MainWindowVisibility { get; set; }
        
        // This is to be used for determining the visibility status of the main window instead of MainWindowVisibility
        // because it is more accurate and reliable representation than using Visibility as a condition check
        public bool MainWindowVisibilityStatus { get; set; } = true;

        public event VisibilityChangedEventHandler VisibilityChanged;

        public Visibility ClockPanelVisibility { get; set; }
        public Visibility SearchIconVisibility { get; set; }
        public double ClockPanelOpacity { get; set; } = 1;
        public double SearchIconOpacity { get; set; } = 1;

        private string _placeholderText;
        public string PlaceholderText
        {
            get => string.IsNullOrEmpty(_placeholderText) ? App.API.GetTranslation("queryTextBoxPlaceholder") : _placeholderText;
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

        public void Query(bool searchDelay, bool isReQuery = false)
        {
            _ = QueryAsync(searchDelay, isReQuery);
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
                var results = PluginManager.GetContextMenusForPlugin(selected);
                results.Add(ContextMenuTopMost(selected));
                results.Add(ContextMenuPluginInfo(selected.PluginID));

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

            var results = new List<Result>();
            foreach (var h in _history.Items)
            {
                var title = App.API.GetTranslation("executeQuery");
                var time = App.API.GetTranslation("lastExecuteTime");
                var result = new Result
                {
                    Title = string.Format(title, h.Query),
                    SubTitle = string.Format(time, h.ExecutedDateTime),
                    IcoPath = "Images\\history.png",
                    Preview = new Result.PreviewInfo
                    {
                        PreviewImagePath = Constant.HistoryIcon,
                        Description = string.Format(time, h.ExecutedDateTime)
                    },
                    OriginQuery = new Query { RawQuery = h.Query },
                    Action = _ =>
                    {
                        SelectedResults = Results;
                        App.API.ChangeQuery(h.Query);
                        return false;
                    }
                };
                results.Add(result);
            }

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

        private async Task QueryResultsAsync(bool searchDelay, bool isReQuery = false, bool reSelect = true)
        {
            _updateSource?.Cancel();

            var query = ConstructQuery(QueryText, Settings.CustomShortcuts, Settings.BuiltinShortcuts);

            var plugins = PluginManager.ValidPluginsForQuery(query);

            if (query == null || plugins.Count == 0) // shortcut expanded
            {
                Results.Clear();
                Results.Visibility = Visibility.Collapsed;
                PluginIconPath = null;
                PluginIconSource = null;
                SearchIconVisibility = Visibility.Visible;
                return;
            }
            else if (plugins.Count == 1)
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

            _updateSource?.Dispose();

            var currentUpdateSource = new CancellationTokenSource();
            _updateSource = currentUpdateSource;
            _updateToken = _updateSource.Token;

            ProgressBarVisibility = Visibility.Hidden;
            _isQueryRunning = true;

            // Switch to ThreadPool thread
            await TaskScheduler.Default;

            if (_updateSource.Token.IsCancellationRequested)
                return;

            // Update the query's IsReQuery property to true if this is a re-query
            query.IsReQuery = isReQuery;

            // handle the exclusiveness of plugin using action keyword
            RemoveOldQueryResults(query);

            _lastQuery = query;

            if (string.IsNullOrEmpty(query.ActionKeyword))
            {
                // Wait 15 millisecond for query change in global query
                // if query changes, return so that it won't be calculated
                await Task.Delay(15, _updateSource.Token);
                if (_updateSource.Token.IsCancellationRequested)
                    return;
            }

            _ = Task.Delay(200, _updateSource.Token).ContinueWith(_ =>
                {
                    // start the progress bar if query takes more than 200 ms and this is the current running query and it didn't finish yet
                    if (!_updateSource.Token.IsCancellationRequested && _isQueryRunning)
                    {
                        ProgressBarVisibility = Visibility.Visible;
                    }
                },
                _updateSource.Token,
                TaskContinuationOptions.NotOnCanceled,
                TaskScheduler.Default);

            // plugins are ICollection, meaning LINQ will get the Count and preallocate Array

            var tasks = plugins.Select(plugin => plugin.Metadata.Disabled switch
            {
                false => QueryTaskAsync(plugin, _updateSource.Token),
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

            if (_updateSource.Token.IsCancellationRequested)
                return;

            // this should happen once after all queries are done so progress bar should continue
            // until the end of all querying
            _isQueryRunning = false;
            if (!_updateSource.Token.IsCancellationRequested)
            {
                // update to hidden if this is still the current query
                ProgressBarVisibility = Visibility.Hidden;
            }

            // Local function
            async Task QueryTaskAsync(PluginPair plugin, CancellationToken token)
            {
                if (searchDelay)
                {
                    var searchDelayTime = plugin.Metadata.SearchDelayTime ?? Settings.SearchDelayTime;

                    await Task.Delay(searchDelayTime, token);

                    if (token.IsCancellationRequested)
                        return;
                }

                // Since it is wrapped within a ThreadPool Thread, the synchronous context is null
                // Task.Yield will force it to run in ThreadPool
                await Task.Yield();

                var results = await PluginManager.QueryForPluginAsync(plugin, query, token);

                if (token.IsCancellationRequested)
                    return;

                IReadOnlyList<Result> resultsCopy;
                if (results == null)
                {
                    resultsCopy = _emptyResult;
                }
                else
                {
                    // make a copy of results to avoid possible issue that FL changes some properties of the records, like score, etc.
                    resultsCopy = CheckQuickSwitchAndDeepClone(results, token);
                }

                foreach (var result in resultsCopy)
                {
                    if (string.IsNullOrEmpty(result.BadgeIcoPath))
                    {
                        result.BadgeIcoPath = plugin.Metadata.IcoPath;
                    }
                }

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(resultsCopy, plugin.Metadata, query,
                    token, reSelect)))
                {
                    App.API.LogError(ClassName, "Unable to add item to Result Update Queue");
                }
            }
        }

        private Query ConstructQuery(string queryText, IEnumerable<CustomShortcutModel> customShortcuts,
            IEnumerable<BuiltinShortcutModel> builtInShortcuts)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return null;
            }

            StringBuilder queryBuilder = new(queryText);
            StringBuilder queryBuilderTmp = new(queryText);

            // Sorting order is important here, the reason is for matching longest shortcut by default
            foreach (var shortcut in customShortcuts.OrderByDescending(x => x.Key.Length))
            {
                if (queryBuilder.Equals(shortcut.Key))
                {
                    queryBuilder.Replace(shortcut.Key, shortcut.Expand());
                }

                queryBuilder.Replace('@' + shortcut.Key, shortcut.Expand());
            }

            string customExpanded = queryBuilder.ToString();

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var shortcut in builtInShortcuts)
                {
                    try
                    {
                        if (customExpanded.Contains(shortcut.Key))
                        {
                            var expansion = shortcut.Expand();
                            queryBuilder.Replace(shortcut.Key, expansion);
                            queryBuilderTmp.Replace(shortcut.Key, expansion);
                        }
                    }
                    catch (Exception e)
                    {
                        App.API.LogException(ClassName,
                            $"Error when expanding shortcut {shortcut.Key}",
                            e);
                    }
                }
            });

            // show expanded builtin shortcuts
            // use private field to avoid infinite recursion
            _queryText = queryBuilderTmp.ToString();

            var query = QueryBuilder.Build(queryBuilder.ToString().Trim(), PluginManager.NonGlobalPlugins);
            return query;
        }

        private void RemoveOldQueryResults(Query query)
        {
            if (_lastQuery?.ActionKeyword != query?.ActionKeyword)
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
                    Title = App.API.GetTranslation("cancelTopMostInThisQuery"),
                    IcoPath = "Images\\down.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.Remove(result);
                        App.API.ShowMsg(App.API.GetTranslation("success"));
                        App.API.ReQuery();
                        return false;
                    }
                };
            }
            else
            {
                menu = new Result
                {
                    Title = App.API.GetTranslation("setAsTopMostInThisQuery"),
                    IcoPath = "Images\\up.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xeac2"),
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.AddOrUpdate(result);
                        App.API.ShowMsg(App.API.GetTranslation("success"));
                        App.API.ReQuery();
                        return false;
                    }
                };
            }

            return menu;
        }

        private static Result ContextMenuPluginInfo(string id)
        {
            var metadata = PluginManager.GetPluginForId(id).Metadata;
            var translator = App.API;

            var author = translator.GetTranslation("author");
            var website = translator.GetTranslation("website");
            var version = translator.GetTranslation("version");
            var plugin = translator.GetTranslation("plugin");
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
                }
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

        #region Quick Switch

        public bool IsQuickSwitch { get; private set; }
        public nint DialogWindowHandle { get; private set; } = nint.Zero;

        public void SetupQuickSwitch(nint handle)
        {
            DialogWindowHandle = handle;
            IsQuickSwitch = true;
            Show();
        }

        public void ResetQuickSwitch()
        {
            DialogWindowHandle = nint.Zero;
            IsQuickSwitch = false;
            Hide();
        }

        #endregion

        #region Public Methods

#pragma warning disable VSTHRD100 // Avoid async void methods

        public void Show()
        {
            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    // 📌 Remove DWM Cloak (Make the window visible normally)
                    Win32Helper.DWMSetCloakForWindow(mainWindow, false);

                    // Set clock and search icon opacity
                    var opacity = (Settings.UseAnimation && !IsQuickSwitch) ? 0.0 : 1.0;
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
            if (StartWithEnglishMode && !IsQuickSwitch)
            {
                Win32Helper.SwitchToEnglishKeyboardLayout(true);
            }
        }

        public async void Hide()
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

            // When application is exiting, the Application.Current will be null
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // When application is exiting, the Application.Current will be null
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    // Set clock and search icon opacity
                    var opacity = (Settings.UseAnimation && !IsQuickSwitch) ? 0.0 : 1.0;
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
            if (StartWithEnglishMode && !IsQuickSwitch)
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
            _topMostRecordStorage.Save();
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
                    if (_topMostRecord.IsTopMost(result))
                    {
                        result.Score = Result.MaxScore;
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
                    _resultsUpdateChannelWriter?.Complete();
                    if (_resultsViewUpdateTask?.IsCompleted == true)
                    {
                        _resultsViewUpdateTask.Dispose();
                    }
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
