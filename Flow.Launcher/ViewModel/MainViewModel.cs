using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
using System.Text;
using System.Threading.Channels;
using ISavable = Flow.Launcher.Plugin.ISavable;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Windows.Input;
using System.ComponentModel;
using Flow.Launcher.Infrastructure.Image;
using System.Windows.Media;

namespace Flow.Launcher.ViewModel
{
    public partial class MainViewModel : BaseModel, ISavable
    {
        #region Private Fields

        private bool _isQueryRunning;
        private Query _lastQuery;
        private Result lastContextMenuResult = new Result();
        private List<Result> lastContextMenuResults = new List<Result>();
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

            Settings = settings;
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

            Results.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Results.SelectedItem):
                        UpdatePreview();
                        break;
                }
            };

            RegisterViewUpdate();
            RegisterResultsUpdatedEvent();
            _ = RegisterClockAndDateUpdateAsync();
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
            }

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

                    // make a copy of results to avoid plugin change the result when updating view model
                    var resultsCopy = e.Results.ToList();

                    PluginManager.UpdatePluginMetadata(resultsCopy, pair.Metadata, e.Query);
                    if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(resultsCopy, pair.Metadata, e.Query,
                            token)))
                    {
                        Log.Error("MainViewModel", "Unable to add item to Result Update Queue");
                    }
                };
            }
        }

        [RelayCommand]
        private async Task ReloadPluginDataAsync()
        {
            Hide();

            await PluginManager.ReloadDataAsync().ConfigureAwait(false);
            Notification.Show(InternationalizationManager.Instance.GetTranslation("success"),
                InternationalizationManager.Instance.GetTranslation("completedSuccessfully"));
        }

        [RelayCommand]
        private void LoadHistory()
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
        }

        [RelayCommand]
        public void ReQuery()
        {
            if (SelectedIsFromQueryResults())
            {
                QueryResults(isReQuery: true);
            }
        }

        public void ReQuery(bool reselect)
        {
            if (SelectedIsFromQueryResults())
            {
                QueryResults(isReQuery: true, reSelect: reselect);
            }
        }

        [RelayCommand]
        public void ReverseHistory()
        {
            if (_history.Items.Count > 0)
            {
                ChangeQueryText(_history.Items[_history.Items.Count - lastHistoryIndex].Query.ToString());
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
                ChangeQueryText(_history.Items[_history.Items.Count - lastHistoryIndex].Query.ToString());
                if (lastHistoryIndex > 1)
                {
                    lastHistoryIndex--;
                }
            }
        }

        [RelayCommand]
        private void LoadContextMenu()
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
            if (result != null && SelectedIsFromQueryResults()) // SelectedItem returns null if selection is empty.
            {
                var autoCompleteText = result.Title;

                if (!string.IsNullOrEmpty(result.AutoCompleteText))
                {
                    autoCompleteText = result.AutoCompleteText;
                }
                else if (!string.IsNullOrEmpty(SelectedResults.SelectedItem?.QuerySuggestionText))
                {
                    var defaultSuggestion = SelectedResults.SelectedItem.QuerySuggestionText;
                    // check if result.actionkeywordassigned is empty
                    if (!string.IsNullOrEmpty(result.ActionKeywordAssigned))
                    {
                        autoCompleteText = $"{result.ActionKeywordAssigned} {defaultSuggestion}";
                    }

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

            var hideWindow = await result.ExecuteAsync(new ActionContext
                {
                    // not null means pressing modifier key + number, should ignore the modifier key
                    SpecialKeyState = index is not null ? SpecialKeyState.Default : GlobalHotkey.CheckModifiers()
                })
                .ConfigureAwait(false);


            if (SelectedIsFromQueryResults())
            {
                _userSelectedRecord.Add(result);
                _history.Add(result.OriginQuery.RawQuery);
                lastHistoryIndex = 1;
            }

            if (hideWindow)
            {
                Hide();
            }
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
            PluginManager.API.OpenUrl("https://www.flowlauncher.com/docs/#/usage-tips");
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
                && SelectedIsFromQueryResults())
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
            if (!SelectedIsFromQueryResults())
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
            if (!SelectedIsFromQueryResults())
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
                Query();
            }
        }

        [RelayCommand]
        private void IncreaseWidth()
        {
            Settings.WindowSize += 100;
            Settings.WindowLeft -= 50;
            OnPropertyChanged(nameof(MainWindowWidth));
        }

        [RelayCommand]
        private void DecreaseWidth()
        {
            if (MainWindowWidth - 100 < 400 || Settings.WindowSize == 400)
            {
                Settings.WindowSize = 400;
            }
            else
            {
                Settings.WindowLeft += 50;
                Settings.WindowSize -= 100;
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (QueryText != queryText)
                {
                    // re-query is done in QueryText's setter method
                    QueryText = queryText;
                    // set to false so the subsequent set true triggers
                    // PropertyChanged and MoveQueryTextToEnd is called
                    QueryTextCursorMovedToEnd = false;
                }
                else if (isReQuery)
                {
                    Query(isReQuery: true);
                }

                QueryTextCursorMovedToEnd = true;
            });
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
                var isReturningFromContextMenu = ContextMenuSelected();
                _selectedResults = value;
                if (SelectedIsFromQueryResults())
                {
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
                }
                else
                {
                    Results.Visibility = Visibility.Collapsed;
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

                _selectedResults.Visibility = Visibility.Visible;
            }
        }

        public Visibility ProgressBarVisibility { get; set; }
        public Visibility MainWindowVisibility { get; set; }
        public double MainWindowOpacity { get; set; } = 1;

        // This is to be used for determining the visibility status of the mainwindow instead of MainWindowVisibility
        // because it is more accurate and reliable representation than using Visibility as a condition check
        public bool MainWindowVisibilityStatus { get; set; } = true;

        public event VisibilityChangedEventHandler VisibilityChanged;

        public Visibility SearchIconVisibility { get; set; }

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

        public string VerifyOrSetDefaultHotkey(string hotkey, string defaultHotkey)
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


        public string Image => Constant.QueryTextBoxIconImagePath;

        public bool StartWithEnglishMode => Settings.AlwaysStartEn;

        #endregion

        #region Preview

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
                Log.Error("MainViewModel", "ResultAreaColumnPreviewHidden/ResultAreaColumnPreviewShown int value not implemented", "InternalPreviewVisible");
#endif
                return false;
            }
        }

        private static readonly int ResultAreaColumnPreviewShown = 1;

        private static readonly int ResultAreaColumnPreviewHidden = 3;

        public int ResultAreaColumn { get; set; } = ResultAreaColumnPreviewShown;

        // This is not a reliable indicator of whether external preview is visible due to the
        // ability of manually closing/exiting the external preview program which, does not inform flow that
        // preview is no longer available.
        public bool ExternalPreviewVisible { get; set; } = false;

        private void ShowPreview()
        {
            var useExternalPreview = PluginManager.UseExternalPreview();

            switch (useExternalPreview)
            {
                case true
                    when CanExternalPreviewSelectedResult(out var path):
                    // Internal preview may still be on when user switches to external
                    if (InternalPreviewVisible)
                        HideInternalPreview();
                    OpenExternalPreview(path);
                    break;

                case true
                    when !CanExternalPreviewSelectedResult(out var _):
                    if (ExternalPreviewVisible)
                        CloseExternalPreview();
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
                CloseExternalPreview();

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
                ShowPreview();
            }
        }

        private void ToggleInternalPreview()
        {
            if (!InternalPreviewVisible)
            {
                ShowInternalPreview();
            }
            else
            {
                HideInternalPreview();
            }
        }

        private void OpenExternalPreview(string path, bool sendFailToast = true)
        {
            _ = PluginManager.OpenExternalPreviewAsync(path, sendFailToast).ConfigureAwait(false);
            ExternalPreviewVisible = true;
        }

        private void CloseExternalPreview()
        {
            _ = PluginManager.CloseExternalPreviewAsync().ConfigureAwait(false);
            ExternalPreviewVisible = false;
        }

        private void SwitchExternalPreview(string path, bool sendFailToast = true)
        {
            _ = PluginManager.SwitchExternalPreviewAsync(path,sendFailToast).ConfigureAwait(false);
        }

        private void ShowInternalPreview()
        {
            ResultAreaColumn = ResultAreaColumnPreviewShown;
            Results.SelectedItem?.LoadPreviewImage();
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
                    OpenExternalPreview(path);
                    break;

                case true:
                    ShowInternalPreview();
                    break;

                case false:
                    HidePreview();
                    break;
            }
        }

        private void UpdatePreview()
        {
            switch (PluginManager.UseExternalPreview())
            {
                case true
                    when CanExternalPreviewSelectedResult(out var path):
                    if (ExternalPreviewVisible)
                    {
                        SwitchExternalPreview(path, false);
                    }
                    else if (InternalPreviewVisible)
                    {
                        HideInternalPreview();
                        OpenExternalPreview(path);
                    }
                    break;

                case true
                    when !CanExternalPreviewSelectedResult(out var _):
                    if (ExternalPreviewVisible)
                    {
                        CloseExternalPreview();
                        ShowInternalPreview();
                    }
                    break;

                case false
                    when InternalPreviewVisible:
                    Results.SelectedItem?.LoadPreviewImage();
                    break;
            }
        }

        private bool CanExternalPreviewSelectedResult(out string path)
        {
            path = Results.SelectedItem?.Result?.Preview.FilePath;
            return !string.IsNullOrEmpty(path);
        }

        #endregion

        #region Query

        public void Query(bool isReQuery = false)
        {
            if (SelectedIsFromQueryResults())
            {
                QueryResults(isReQuery);
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
                if (selected == lastContextMenuResult)
                {
                    results = lastContextMenuResults;
                }
                else
                {
                    results = PluginManager.GetContextMenusForPlugin(selected);
                    lastContextMenuResults = results;
                    lastContextMenuResult = selected;
                    results.Add(ContextMenuTopMost(selected));
                    results.Add(ContextMenuPluginInfo(selected.PluginID));
                }


                if (!string.IsNullOrEmpty(query))
                {
                    var filtered = results.Select(x => x.Clone()).Where
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
                    OriginQuery = new Query { RawQuery = h.Query },
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

        private async void QueryResults(bool isReQuery = false, bool reSelect = true)
        {
            _updateSource?.Cancel();

            var query = ConstructQuery(QueryText, Settings.CustomShortcuts, Settings.BuiltinShortcuts);

            if (query == null) // shortcut expanded
            {
                Results.Clear();
                Results.Visibility = Visibility.Collapsed;
                PluginIconPath = null;
                PluginIconSource = null;
                SearchIconVisibility = Visibility.Visible;
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

            // Update the query's IsReQuery property to true if this is a re-query
            query.IsReQuery = isReQuery;

            // handle the exclusiveness of plugin using action keyword
            RemoveOldQueryResults(query);

            _lastQuery = query;

            var plugins = PluginManager.ValidPluginsForQuery(query);

            if (plugins.Count == 1)
            {
                PluginIconPath = plugins.Single().Metadata.IcoPath;
                PluginIconSource = await ImageLoader.LoadAsync(PluginIconPath);
                SearchIconVisibility = Visibility.Hidden;
            }
            else
            {
                PluginIconPath = null;
                PluginIconSource = null;
                SearchIconVisibility = Visibility.Visible;
            }


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
                false => QueryTask(plugin, reSelect),
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
            async Task QueryTask(PluginPair plugin, bool reSelect = true)
            {
                // Since it is wrapped within a ThreadPool Thread, the synchronous context is null
                // Task.Yield will force it to run in ThreadPool
                await Task.Yield();

                IReadOnlyList<Result> results =
                    await PluginManager.QueryForPluginAsync(plugin, query, currentCancellationToken);

                currentCancellationToken.ThrowIfCancellationRequested();

                results ??= _emptyResult;

                if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(results, plugin.Metadata, query,
                        currentCancellationToken, reSelect)))
                {
                    Log.Error("MainViewModel", "Unable to add item to Result Update Queue");
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
                        Log.Exception(
                            $"{nameof(MainViewModel)}.{nameof(ConstructQuery)}|Error when expanding shortcut {shortcut.Key}",
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
                    Title = InternationalizationManager.Instance.GetTranslation("cancelTopMostInThisQuery"),
                    IcoPath = "Images\\down.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ =>
                    {
                        _topMostRecord.Remove(result);
                        App.API.ShowMsg(InternationalizationManager.Instance.GetTranslation("success"));
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
                        App.API.ShowMsg(InternationalizationManager.Instance.GetTranslation("success"));
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

        public void Show()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindowVisibility = Visibility.Visible;

                MainWindowOpacity = 1;

                MainWindowVisibilityStatus = true;
                VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs { IsVisible = true });
            });
        }

        public async void Hide()
        {
            lastHistoryIndex = 1;
            // Trick for no delay
            MainWindowOpacity = 0;
            lastContextMenuResult = new Result();
            lastContextMenuResults = new List<Result>();

            if (ExternalPreviewVisible)
                CloseExternalPreview();

            if (!SelectedIsFromQueryResults())
            {
                SelectedResults = Results;
            }

            switch (Settings.LastQueryMode)
            {
                case LastQueryMode.Empty:
                    ChangeQueryText(string.Empty);
                    await Task.Delay(100); //Time for change to opacity
                    break;
                case LastQueryMode.Preserved:
                    if (Settings.UseAnimation)
                        await Task.Delay(100);
                    LastQuerySelected = true;
                    break;
                case LastQueryMode.Selected:
                    if (Settings.UseAnimation)
                        await Task.Delay(100);
                    LastQuerySelected = false;
                    break;
                case LastQueryMode.ActionKeywordPreserved or LastQueryMode.ActionKeywordSelected:
                    var newQuery = _lastQuery.ActionKeyword;
                    if (!string.IsNullOrEmpty(newQuery))
                        newQuery += " ";
                    ChangeQueryText(newQuery);
                    if (Settings.UseAnimation)
                        await Task.Delay(100);
                    if (Settings.LastQueryMode == LastQueryMode.ActionKeywordSelected)
                        LastQuerySelected = false;
                    break;
                default:
                    throw new ArgumentException($"wrong LastQueryMode: <{Settings.LastQueryMode}>");
            }

            MainWindowVisibilityStatus = false;
            MainWindowVisibility = Visibility.Collapsed;
            VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs { IsVisible = false });
        }

        /// <summary>
        /// Checks if Flow Launcher should ignore any hotkeys
        /// </summary>
        public bool ShouldIgnoreHotkeys()
        {
            return Settings.IgnoreHotkeysOnFullscreen && WindowsInteropHelper.IsWindowFullscreen() || GameModeStatus;
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
                    else if (result.Score != Result.MaxScore)
                    {
                        var priorityScore = metaResults.Metadata.Priority * 150;
                        result.Score += result.AddSelectedCount ?
                            _userSelectedRecord.GetSelectedCount(result) + priorityScore :
                            priorityScore;
                    }
                }
            }

            // it should be the same for all results
            bool reSelect = resultsForUpdates.First().ReSelectFirstResult;

            Results.AddResults(resultsForUpdates, token, reSelect);
        }

        #endregion
    }
}
