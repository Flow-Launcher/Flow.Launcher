using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Storage;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Storage;
using Dispatcher = Avalonia.Threading.Dispatcher;
using FontStyle = Avalonia.Media.FontStyle;
using FontWeight = Avalonia.Media.FontWeight;
using IImage = Avalonia.Media.IImage;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// Represents which view is currently active.
/// </summary>
public enum ActiveView
{
    Results,
    ContextMenu,
    History
}

public enum QueryTextFocusMode
{
    Focus,
    SelectAll,
    CaretAtEnd
}

public readonly record struct QueryTextFocusRequest(bool ShowWindow, bool ActivateWindow, QueryTextFocusMode Mode);

/// <summary>
/// MainViewModel for Avalonia - port of the WPF MainViewModel query, history and context menu behaviour.
/// </summary>
public partial class MainViewModel : ObservableObject, IResultUpdateRegister
{
    private static readonly string ClassName = nameof(MainViewModel);
    private const int ProgressBarDelayMilliseconds = 200;
    private const int MinMaxResultsToShow = 2;
    private const int MaxMaxResultsToShow = 17;
    private const string SegoeFluentIcons = "/Resources/#Segoe Fluent Icons";
    private static readonly GlyphInfo HistoryGlyph = new(FontFamily: SegoeFluentIcons, Glyph: "\uE81C");

    private readonly Settings _settings;
    private readonly FlowLauncherJsonStorage<History> _historyItemsStorage;
    private readonly History _history;
    private readonly FlowLauncherJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
    private readonly UserSelectedRecord _userSelectedRecord;
    private readonly FlowLauncherJsonStorageTopMostRecord _topMostRecord;
    private CancellationTokenSource? _queryTokenSource;
    private string? _ignoredQueryText;
    private string _queryTextBeforeLeaveResults = string.Empty;
    private bool _pluginsReady;
    private int _lastHistoryIndex = 1;
    private string _currentQueryOriginalText = string.Empty;
    private string _lastQueryActionKeyword = string.Empty;
    private CancellationTokenSource? _progressBarDelayTokenSource;

    // The result (and the view it came from) whose context menu is open, and the unfiltered menu items.
    private ContextMenuSource? _contextMenuSource;
    private List<Result> _contextMenuItems = [];

    // Full query text the autocomplete hotkey falls back to (the ghost suggestion shown behind the query box).
    private string _querySuggestionCompletion = string.Empty;

    // Preview visibility chosen with the preview hotkey; null follows Settings.AlwaysPreview. Reset on every show.
    private bool? _previewPreferenceFromToggle;

    // Channel-based debouncing for result updates (matches WPF approach)
    private readonly Channel<ResultsForUpdate> _resultsUpdateChannel;
    private readonly ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
    private readonly Task _resultsViewUpdateTask;

    private sealed record ContextMenuSource(ActiveView View, string QueryText, ResultViewModel Selected, int SelectedIndex);

    public event Action? HideRequested;
    public event Action<QueryTextFocusRequest>? QueryTextFocusRequested;
    public event VisibilityChangedEventHandler? VisibilityChanged;

    [ObservableProperty]
    private bool _mainWindowVisibility = false;

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private bool _isQueryRunning;

    [ObservableProperty]
    private bool _isProgressBarVisible;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private ResultsViewModel _results;

    [ObservableProperty]
    private ResultsViewModel _contextMenu;

    [ObservableProperty]
    private ResultsViewModel _historyView;

    [ObservableProperty]
    private ActiveView _activeView = ActiveView.Results;

    [ObservableProperty]
    private ResultViewModel? _previewSelectedItem;

    [ObservableProperty]
    private bool _isPreviewOn;

    [ObservableProperty]
    private bool _gameModeStatus;

    [ObservableProperty]
    private string _clockText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    /// <summary>
    /// Ghost text drawn behind the query box: the query text followed by the rest of the selected result's suggestion.
    /// </summary>
    [ObservableProperty]
    private string _querySuggestionText = string.Empty;

    /// <summary>
    /// Icon of the plugin when the query's action keyword selects a single plugin; replaces the search icon.
    /// </summary>
    [ObservableProperty]
    private string? _pluginIconPath;

    /// <summary>
    /// Whether the results view is currently active.
    /// </summary>
    public bool IsResultsViewActive => ActiveView == ActiveView.Results;

    /// <summary>
    /// Whether the context menu view is currently active.
    /// </summary>
    public bool IsContextMenuViewActive => ActiveView == ActiveView.ContextMenu;

    /// <summary>
    /// Whether the history view is currently active.
    /// </summary>
    public bool IsHistoryViewActive => ActiveView == ActiveView.History;

    /// <summary>
    /// Whether to show the results/context menu area (separator + list).
    /// A typed query keeps the area open regardless of the collection count to prevent flickering; an empty query
    /// shows it only when the home page produced results.
    /// </summary>
    public bool ShowResultsArea => ActiveView != ActiveView.Results
        || !string.IsNullOrWhiteSpace(QueryText)
        || HasResults;

    /// <summary>
    /// The clock/date panel is shown while the query box is empty in the results view.
    /// </summary>
    public bool IsClockPanelVisible => ActiveView == ActiveView.Results && string.IsNullOrEmpty(QueryText);

    public Task<IImage?>? PluginIconImage { get; private set; }

    public bool IsPluginIconVisible => !string.IsNullOrEmpty(PluginIconPath);

    public bool IsSearchIconVisible => string.IsNullOrEmpty(PluginIconPath);

    public string PlaceholderText => !_settings.ShowPlaceholder
        ? string.Empty
        : string.IsNullOrEmpty(_settings.PlaceholderText) ? Translate("queryTextBoxPlaceholder") : _settings.PlaceholderText;

    public FontWeight QueryBoxFontWeight =>
        Enum.TryParse<FontWeight>(_settings.QueryBoxFontWeight, true, out var weight) ? weight : FontWeight.Normal;

    public FontStyle QueryBoxFontStyle =>
        Enum.TryParse<FontStyle>(_settings.QueryBoxFontStyle, true, out var style) ? style : FontStyle.Normal;

    public Settings Settings => _settings;

    public bool LastQuerySelected { get; set; }

    private ResultsViewModel ActiveResults => ActiveView switch
    {
        ActiveView.ContextMenu => ContextMenu,
        ActiveView.History => HistoryView,
        _ => Results
    };

    public MainViewModel(Settings settings)
    {
        _settings = settings;
        _historyItemsStorage = new FlowLauncherJsonStorage<History>();
        _history = _historyItemsStorage.Load();
        _userSelectedRecordStorage = new FlowLauncherJsonStorage<UserSelectedRecord>();
        _userSelectedRecord = _userSelectedRecordStorage.Load();
        _topMostRecord = new FlowLauncherJsonStorageTopMostRecord();
        _results = new ResultsViewModel(settings);
        _contextMenu = new ResultsViewModel(settings);
        _historyView = new ResultsViewModel(settings);

        // Initialize channel-based debouncing for result updates
        _resultsUpdateChannel = Channel.CreateUnbounded<ResultsForUpdate>();
        _resultsUpdateChannelWriter = _resultsUpdateChannel.Writer;
        _resultsViewUpdateTask = Task.Run(ProcessResultUpdatesAsync);

        _results.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResultsViewModel.SelectedItem) && IsResultsViewActive)
            {
                PreviewSelectedItem = _results.SelectedItem;
                UpdateQuerySuggestion();
            }
        };

        _contextMenu.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResultsViewModel.SelectedItem) && IsContextMenuViewActive)
            {
                PreviewSelectedItem = _contextMenu.SelectedItem;
            }
        };

        _historyView.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResultsViewModel.SelectedItem) && IsHistoryViewActive)
            {
                PreviewSelectedItem = _historyView.SelectedItem;
            }
        };

        _settings.PropertyChanged += OnSettingsPropertyChanged;

        UpdateClock();
        _ = RunClockAsync();
    }

    /// <summary>
    /// Background task that processes result updates with leading-edge batching:
    /// the first update is applied immediately, updates arriving within the next frame (~16ms) are coalesced.
    /// </summary>
    private async Task ProcessResultUpdatesAsync()
    {
        var channelReader = _resultsUpdateChannel.Reader;

        while (await channelReader.WaitToReadAsync())
        {
            try
            {
                var pendingUpdates = new List<ResultsForUpdate>();

                while (channelReader.TryRead(out var update))
                {
                    if (!update.Token.IsCancellationRequested)
                    {
                        pendingUpdates.Add(update);
                    }
                }

                if (pendingUpdates.Count == 0)
                {
                    continue;
                }

                var latestFullUpdateIndex = pendingUpdates.FindLastIndex(update => !update.IsPluginUpdate);
                if (latestFullUpdateIndex >= 0)
                {
                    await ApplyResultsUpdateAsync(pendingUpdates[latestFullUpdateIndex]);
                    for (var i = latestFullUpdateIndex + 1; i < pendingUpdates.Count; i++)
                    {
                        await ApplyResultsUpdateAsync(pendingUpdates[i]);
                    }
                }
                else
                {
                    foreach (var update in pendingUpdates)
                    {
                        await ApplyResultsUpdateAsync(update);
                    }
                }

                // Coalesce updates that arrive within the next frame into the following batch
                await Task.Delay(16);
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, "Error processing result update batch", e);
            }
        }
    }

    private async Task ApplyResultsUpdateAsync(ResultsForUpdate update)
    {
        if (update.Token.IsCancellationRequested)
        {
            return;
        }

        var sortedResults = update.Results
            .OrderByDescending(r => r.Score)
            .ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (update.Token.IsCancellationRequested) return;
            if (update.IsPluginUpdate)
            {
                Results.ReplaceResultsForPlugin(update.PluginId!, sortedResults);
            }
            else
            {
                Results.ReplaceResults(sortedResults, update.ReselectFirst);
            }

            HasResults = Results.Results.Count > 0;
        });
    }

    partial void OnActiveViewChanged(ActiveView value)
    {
        OnPropertyChanged(nameof(IsResultsViewActive));
        OnPropertyChanged(nameof(IsContextMenuViewActive));
        OnPropertyChanged(nameof(IsHistoryViewActive));
        OnPropertyChanged(nameof(ShowResultsArea));
        OnPropertyChanged(nameof(IsClockPanelVisible));

        PreviewSelectedItem = value switch
        {
            ActiveView.Results => Results.SelectedItem,
            ActiveView.ContextMenu => ContextMenu.SelectedItem,
            ActiveView.History => HistoryView.SelectedItem,
            _ => null,
        };

        UpdateQuerySuggestion();
    }

    partial void OnHasResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowResultsArea));
    }

    partial void OnPreviewSelectedItemChanged(ResultViewModel? value)
    {
        UpdatePreviewVisibility();
    }

    partial void OnPluginIconPathChanged(string? value)
    {
        PluginIconImage = string.IsNullOrEmpty(value) ? null : ImageLoader.LoadAsync(value);
        OnPropertyChanged(nameof(PluginIconImage));
        OnPropertyChanged(nameof(IsPluginIconVisible));
        OnPropertyChanged(nameof(IsSearchIconVisible));
    }

    partial void OnIsQueryRunningChanged(bool value)
    {
        if (value)
        {
            StartProgressBarDelay();
        }
        else
        {
            StopProgressBarDelay();
        }
    }

    private void StartProgressBarDelay()
    {
        StopProgressBarDelay();

        var delayTokenSource = new CancellationTokenSource();
        _progressBarDelayTokenSource = delayTokenSource;
        _ = ShowProgressBarAfterDelayAsync(delayTokenSource.Token);
    }

    private void StopProgressBarDelay()
    {
        _progressBarDelayTokenSource?.Cancel();
        _progressBarDelayTokenSource?.Dispose();
        _progressBarDelayTokenSource = null;
        IsProgressBarVisible = false;
    }

    private async Task ShowProgressBarAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ProgressBarDelayMilliseconds, token);
            if (!token.IsCancellationRequested && IsQueryRunning)
            {
                IsProgressBarVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSettingsPropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(Settings.ShowPlaceholder):
            case nameof(Settings.PlaceholderText):
                OnPropertyChanged(nameof(PlaceholderText));
                break;
            case nameof(Settings.Language):
                OnPropertyChanged(nameof(PlaceholderText));
                RequeryHomePage();
                break;
            case nameof(Settings.QueryBoxFontWeight):
                OnPropertyChanged(nameof(QueryBoxFontWeight));
                break;
            case nameof(Settings.QueryBoxFontStyle):
                OnPropertyChanged(nameof(QueryBoxFontStyle));
                break;
            case nameof(Settings.UseClock):
            case nameof(Settings.UseDate):
            case nameof(Settings.TimeFormat):
            case nameof(Settings.DateFormat):
                UpdateClock();
                break;
            case nameof(Settings.ShowHomePage):
            case nameof(Settings.ShowHistoryResultsForHomePage):
            case nameof(Settings.MaxHistoryResultsToShowForHomePage):
            case nameof(Settings.HistoryStyle):
                RequeryHomePage();
                break;
            case nameof(Settings.AlwaysPreview):
                UpdatePreviewVisibility();
                break;
        }
    }

    private void RequeryHomePage()
    {
        if (ActiveView == ActiveView.Results && string.IsNullOrEmpty(QueryText))
        {
            _ = QueryAsync();
        }
    }

    #region Clock

    private async Task RunClockAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            Dispatcher.UIThread.Post(UpdateClock);
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        if (_settings.UseClock)
        {
            ClockText = FormatDateTime(now, _settings.TimeFormat);
        }

        if (_settings.UseDate)
        {
            DateText = FormatDateTime(now, _settings.DateFormat);
        }
    }

    private static string FormatDateTime(DateTime value, string format)
    {
        try
        {
            return value.ToString(format, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return value.ToString(CultureInfo.CurrentCulture);
        }
    }

    #endregion

    #region Preview

    [RelayCommand]
    public void TogglePreview()
    {
        _previewPreferenceFromToggle = !IsPreviewOn;
        IsPreviewOn = _previewPreferenceFromToggle.Value;
    }

    // Port of WPF ShouldShowPreview: the result's PreviewVisibility overrides the user's toggle/AlwaysPreview choice.
    private void UpdatePreviewVisibility()
    {
        var item = PreviewSelectedItem;
        if (item == null)
        {
            IsPreviewOn = false;
            return;
        }

        IsPreviewOn = item.PluginResult?.PreviewVisibility switch
        {
            PreviewVisibility.Never => false,
            PreviewVisibility.Always => true,
            _ => _previewPreferenceFromToggle ?? _settings.AlwaysPreview
        };
    }

    #endregion

    public void OnPluginsReady()
    {
        _pluginsReady = true;
        MainWindowVisibility = !_settings.HideOnStartup;
        Log.Info(ClassName, MainWindowVisibility ? "Plugins ready - window shown" : "Plugins ready - window kept hidden");
        // An empty query runs the home page query.
        _ = QueryAsync();
    }

    /// <summary>
    /// Register a plugin to receive results updated event.
    /// Required by IResultUpdateRegister for plugin initialization.
    /// </summary>
    public void RegisterResultsUpdatedEvent(PluginPair pair)
    {
        if (pair.Plugin is not IResultUpdated plugin)
        {
            return;
        }

        plugin.ResultsUpdated += (_, e) =>
        {
            if (e.Token.IsCancellationRequested ||
                !string.Equals(e.Query.OriginalQuery ?? string.Empty, _currentQueryOriginalText, StringComparison.Ordinal))
            {
                return;
            }

            var token = e.Token == default ? _queryTokenSource?.Token ?? CancellationToken.None : e.Token;
            if (token.IsCancellationRequested)
            {
                return;
            }

            // Clone so the plugin can keep changing its own list and items while the view model uses them.
            var resultsCopy = new List<Result>();
            if (e.Results != null)
            {
                foreach (var result in e.Results.ToList())
                {
                    if (token.IsCancellationRequested) return;
                    resultsCopy.Add(result.Clone());
                }
            }

            PluginManager.UpdatePluginMetadata(resultsCopy, pair.Metadata, e.Query);

            if (token.IsCancellationRequested) return;

            var results = CreateResultViewModels(resultsCopy, pair);
            if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(results, token, pair.Metadata.ID)))
            {
                Log.Error(ClassName, "Unable to add item to Result Update Queue");
            }
        };
    }

    public void RequestHide() => Hide();

    public void RequestQueryTextFocus(QueryTextFocusRequest request)
    {
        QueryTextFocusRequested?.Invoke(request);
    }

    /// <summary>
    /// Returns to the query results and queries again, telling plugins not to serve cached results.
    /// </summary>
    public void ReQuery(bool reselect = true)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ReQuery(reselect));
            return;
        }

        BackToResults();
        _ = QueryAsync(reselect: reselect, isReQuery: true);
    }

    /// <summary>
    /// Re-runs the current query (Ctrl+R) when the query results are shown.
    /// </summary>
    public void ReQueryResults()
    {
        if (ActiveView == ActiveView.Results)
        {
            _ = QueryAsync(isReQuery: true);
        }
    }

    public void BackToQueryResults()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(BackToQueryResults);
            return;
        }

        BackToResults();
    }

    /// <summary>
    /// Changes the query text from code: returns to the results view, sets the text without search delay, moves the
    /// caret to the end, and queries again when the text is unchanged and <paramref name="requery"/> is set.
    /// </summary>
    public void ChangeQueryText(string text, bool requery = false)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ChangeQueryText(text, requery));
            return;
        }

        BackToResults();

        if (!string.Equals(QueryText, text, StringComparison.Ordinal))
        {
            SetQueryTextSilently(text);
            UpdateLastQueryActionKeyword(text);
            _ = QueryAsync();
        }
        else if (requery)
        {
            _ = QueryAsync(isReQuery: true);
        }

        RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.CaretAtEnd));
    }

    public void Save()
    {
        _historyItemsStorage.Save();
        _userSelectedRecordStorage.Save();
        _topMostRecord.Save();
    }

    #region History

    [RelayCommand]
    private void ReverseHistory()
    {
        var historyItems = _history.LastOpenedHistoryItems;
        if (historyItems.Count == 0)
        {
            return;
        }

        ChangeQueryText(historyItems[^_lastHistoryIndex].Query);
        if (_lastHistoryIndex < historyItems.Count)
        {
            _lastHistoryIndex++;
        }
    }

    [RelayCommand]
    private void ForwardHistory()
    {
        var historyItems = _history.LastOpenedHistoryItems;
        if (historyItems.Count == 0)
        {
            return;
        }

        ChangeQueryText(historyItems[^_lastHistoryIndex].Query);
        if (_lastHistoryIndex > 1)
        {
            _lastHistoryIndex--;
        }
    }

    [RelayCommand]
    private void LoadHistory()
    {
        if (ActiveView != ActiveView.Results)
        {
            BackToResults();
            return;
        }

        _queryTextBeforeLeaveResults = QueryText;
        ActiveView = ActiveView.History;
        SetQueryTextSilently(string.Empty);
        QueryHistory(string.Empty);

        if (HistoryView.Results.Count > 0)
        {
            HistoryView.SelectedIndex = 0;
            HistoryView.SelectedItem = HistoryView.Results[0];
        }
    }

    private void QueryHistory(string queryText)
    {
        var normalizedQuery = queryText.ToLowerInvariant().Trim();
        var historyResults = CreateHistoryResults(_history.LastOpenedHistoryItems);

        if (!string.IsNullOrEmpty(normalizedQuery))
        {
            historyResults = historyResults.Where(result =>
            {
                var titleMatch = App.API.FuzzySearch(normalizedQuery, result.Title);
                if (titleMatch.IsSearchPrecisionScoreMet())
                {
                    result.Score = titleMatch.Score;
                    return true;
                }

                var subtitleMatch = App.API.FuzzySearch(normalizedQuery, result.SubTitle);
                if (subtitleMatch.IsSearchPrecisionScoreMet())
                {
                    result.Score = subtitleMatch.Score;
                    return true;
                }

                return false;
            }).ToList();
        }

        HistoryView.ReplaceResults(historyResults);
        HasResults = HistoryView.Results.Count > 0;
    }

    /// <summary>
    /// Builds history rows, newest first. The last-opened style shows each result once; the query style shows one
    /// row per executed query.
    /// </summary>
    private List<ResultViewModel> CreateHistoryResults(IEnumerable<LastOpenedHistoryResult> historyItems, int? maxResults = null)
    {
        var lastOpenedStyle = _settings.HistoryStyle == HistoryStyle.LastOpened;
        IEnumerable<LastOpenedHistoryResult> items = historyItems.OrderByDescending(item => item.ExecutedDateTime);

        if (lastOpenedStyle)
        {
            items = items
                .GroupBy(item => (item.Title, item.SubTitle, item.PluginID, item.RecordKey))
                .Select(group => group.First());
        }

        if (maxResults.HasValue)
        {
            items = items.Take(maxResults.Value);
        }

        var itemList = items.ToList();
        return itemList
            .Select((item, index) => CreateHistoryResult(item, itemList.Count - index, lastOpenedStyle))
            .ToList();
    }

    private static ResultViewModel CreateHistoryResult(LastOpenedHistoryResult item, int score, bool lastOpenedStyle)
    {
        return new ResultViewModel
        {
            Title = lastOpenedStyle ? item.Title : FormatTranslation("executeQuery", item.Query),
            SubTitle = FormatTranslation("lastExecuteTime", item.ExecutedDateTime),
            IconPath = lastOpenedStyle ? GetHistoryIconPath(item) : Constant.HistoryIcon,
            Glyph = lastOpenedStyle ? item.Glyph : HistoryGlyph,
            Score = score,
            HistoryItem = item,
        };
    }

    // Relative icons are resolved against the plugin's current directory so they survive Flow being moved.
    private static string GetHistoryIconPath(LastOpenedHistoryResult item)
    {
        var plugin = string.IsNullOrEmpty(item.PluginID) ? null : PluginManager.GetPluginForId(item.PluginID);
        if (string.IsNullOrEmpty(item.IcoPath))
        {
            return plugin?.Metadata.IcoPath ?? Constant.HistoryIcon;
        }

        if (Path.IsPathRooted(item.IcoPath) || item.IcoPath.Contains("://", StringComparison.Ordinal)
            || item.IcoPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return item.IcoPath;
        }

        var directory = plugin?.Metadata.PluginDirectory ?? item.PluginDirectory;
        return string.IsNullOrEmpty(directory) ? item.IcoPath : Path.Combine(directory, item.IcoPath);
    }

    private Task _historySaveTask = Task.CompletedTask;

    /// <summary>
    /// Records the history entry immediately and persists it in the background so hiding never waits on disk I/O.
    /// Saves are chained so concurrent writes never race on the same file.
    /// </summary>
    private void RecordHistory(string executedQueryText, Result result)
    {
        _history.Add(executedQueryText, result);
        _lastHistoryIndex = 1;
        SaveHistoryInBackground();
    }

    private void SaveHistoryInBackground()
    {
        _historySaveTask = SaveHistoryAfterAsync(_historySaveTask);
    }

    private async Task SaveHistoryAfterAsync(Task previousSave)
    {
        await previousSave;
        try
        {
            await _historyItemsStorage.SaveAsync();
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to save history", e);
        }
    }

    private void RecordUserSelected(Result result)
    {
        _userSelectedRecord.Add(result);
        // FlowLauncherJsonStorage.SaveAsync logs its own failures; the wrapper guards against anything thrown before the await.
        _ = Task.Run(async () =>
        {
            try
            {
                await _userSelectedRecordStorage.SaveAsync();
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, "Failed to save user selected record", e);
            }
        });
    }

    #endregion

    #region Show / Hide

    /// <summary>
    /// Toggle the main window visibility. Called by global hotkey.
    /// </summary>
    public void ToggleFlowLauncher()
    {
        Log.Debug(ClassName, "ToggleFlowLauncher called");
        if (MainWindowVisibility)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// Hide the main window.
    /// </summary>
    public void Hide()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Hide);
            return;
        }

        // Restore the results query text first so the last query mode applies to it, not to a menu filter.
        BackToResults();
        PrepareLastQueryModeBeforeHide();
        HideRequested?.Invoke();
        MainWindowVisibility = false;
        ResetTransientViewState();
        ApplyLastQueryModeForHide();
        VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs { IsVisible = false });
        Log.Debug(ClassName, "Hide requested");
    }

    /// <summary>
    /// Show the main window.
    /// </summary>
    public void Show()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Show);
            return;
        }

        ResetTransientViewState();
        var focusMode = ApplyLastQueryModeForShow();
        RequestQueryTextFocus(new QueryTextFocusRequest(true, true, focusMode));
        MainWindowVisibility = true;
        OnShown();
        Log.Debug(ClassName, "Show requested");
    }

    /// <summary>
    /// Show the main window with an injected query.
    /// </summary>
    public void ShowWithInjectedQuery(string queryText)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowWithInjectedQuery(queryText));
            return;
        }

        ResetTransientViewState();
        QueryText = queryText;
        RequestQueryTextFocus(new QueryTextFocusRequest(true, true, QueryTextFocusMode.CaretAtEnd));
        MainWindowVisibility = true;
        OnShown();
        Log.Info(ClassName, "Show with injected query requested");
    }

    private void OnShown()
    {
        _settings.ActivateTimes++;

        _previewPreferenceFromToggle = null;
        UpdatePreviewVisibility();

        // Refresh the home page so history and home results are current every time the window opens.
        if (_pluginsReady && string.IsNullOrEmpty(QueryText) && (_settings.ShowHomePage || _settings.ShowHistoryResultsForHomePage))
        {
            _ = QueryAsync();
        }

        VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs { IsVisible = true });
    }

    private void ResetTransientViewState()
    {
        _lastHistoryIndex = 1;
        _queryTextBeforeLeaveResults = string.Empty;
        _contextMenuSource = null;
        _contextMenuItems = [];
        ActiveView = ActiveView.Results;
        ContextMenu.Clear();
        HistoryView.Clear();
    }

    private void PrepareLastQueryModeBeforeHide()
    {
        if (_settings.LastQueryMode != LastQueryMode.Selected)
        {
            return;
        }

        LastQuerySelected = true;
        RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.SelectAll));
    }

    private void ApplyLastQueryModeForHide()
    {
        switch (_settings.LastQueryMode)
        {
            case LastQueryMode.Empty:
                QueryText = string.Empty;
                break;
            case LastQueryMode.Preserved:
                LastQuerySelected = true;
                break;
            case LastQueryMode.Selected:
                break;
            case LastQueryMode.ActionKeywordPreserved:
                ApplyLastActionKeywordQueryText();
                LastQuerySelected = true;
                break;
            case LastQueryMode.ActionKeywordSelected:
                ApplyLastActionKeywordQueryText();
                LastQuerySelected = true;
                break;
        }
    }

    private QueryTextFocusMode ApplyLastQueryModeForShow()
    {
        switch (_settings.LastQueryMode)
        {
            case LastQueryMode.Empty:
                QueryText = string.Empty;
                return QueryTextFocusMode.SelectAll;
            case LastQueryMode.Preserved:
                LastQuerySelected = true;
                return QueryTextFocusMode.CaretAtEnd;
            case LastQueryMode.Selected:
                return GetLastQueryFocusMode();
            case LastQueryMode.ActionKeywordPreserved:
                ApplyLastActionKeywordQueryText();
                LastQuerySelected = true;
                return QueryTextFocusMode.CaretAtEnd;
            case LastQueryMode.ActionKeywordSelected:
                ApplyLastActionKeywordQueryText();
                return GetLastQueryFocusMode();
            default:
                return QueryTextFocusMode.SelectAll;
        }
    }

    private QueryTextFocusMode GetLastQueryFocusMode()
    {
        if (LastQuerySelected)
        {
            return QueryTextFocusMode.Focus;
        }

        LastQuerySelected = true;
        return QueryTextFocusMode.SelectAll;
    }

    private void ApplyLastActionKeywordQueryText()
    {
        QueryText = string.IsNullOrEmpty(_lastQueryActionKeyword)
            ? string.Empty
            : $"{_lastQueryActionKeyword}{Query.ActionKeywordSeparator}";
    }

    #endregion

    #region View navigation

    /// <summary>
    /// Returns from the context menu or history view to the query results, restoring the query text they replaced.
    /// The results were not re-queried meanwhile, so the text is restored without querying again.
    /// </summary>
    [RelayCommand]
    private void BackToResults()
    {
        if (ActiveView == ActiveView.Results)
        {
            return;
        }

        _contextMenuSource = null;
        _contextMenuItems = [];
        ActiveView = ActiveView.Results;
        ContextMenu.Clear();
        HistoryView.Clear();
        SetQueryTextSilently(_queryTextBeforeLeaveResults);
        _queryTextBeforeLeaveResults = string.Empty;
        HasResults = Results.Results.Count > 0;
        RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.CaretAtEnd));
    }

    [RelayCommand]
    private void Esc()
    {
        switch (ActiveView)
        {
            case ActiveView.ContextMenu:
                ReturnFromContextMenu();
                break;
            case ActiveView.History:
                BackToResults();
                break;
            default:
                Hide();
                break;
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        Hide();
        Dispatcher.UIThread.Post(() => SettingsWindow.Open());
    }

    [RelayCommand]
    public void ToggleGameMode()
    {
        GameModeStatus = !GameModeStatus;
    }

    [RelayCommand]
    private void SelectNextItem()
    {
        ActiveResults.SelectNextItem();
    }

    [RelayCommand]
    private void SelectPrevItem()
    {
        // Up on an empty query box without results cycles through the query history, as in the WPF app.
        if (ActiveView == ActiveView.Results
            && string.IsNullOrEmpty(QueryText)
            && Results.Results.Count == 0
            && _history.LastOpenedHistoryItems.Count > 0)
        {
            _lastHistoryIndex = 1;
            ReverseHistory();
            return;
        }

        ActiveResults.SelectPrevItem();
    }

    [RelayCommand]
    private void SelectNextPage() => ActiveResults.SelectNextPage();

    [RelayCommand]
    private void SelectPrevPage() => ActiveResults.SelectPrevPage();

    [RelayCommand]
    private void SelectFirstResult() => ActiveResults.SelectFirstResult();

    [RelayCommand]
    private void SelectLastResult() => ActiveResults.SelectLastResult();

    [RelayCommand]
    private void IncreaseMaxResult()
    {
        if (_settings.MaxResultsToShow >= MaxMaxResultsToShow)
            return;

        _settings.MaxResultsToShow += 1;
    }

    [RelayCommand]
    private void DecreaseMaxResult()
    {
        if (_settings.MaxResultsToShow <= MinMaxResultsToShow)
            return;

        _settings.MaxResultsToShow -= 1;
    }

    [RelayCommand]
    private async Task ReloadPluginDataAsync()
    {
        Hide();

        await PluginManager.ReloadDataAsync().ConfigureAwait(false);
        App.API?.ShowMsg(Translate("success"), Translate("completedSuccessfully"));
    }

    /// <summary>
    /// Copies the selected query result's copy text (Ctrl+Shift+C).
    /// </summary>
    [RelayCommand]
    private void CopyAlternative()
    {
        var copyText = Results.SelectedItem?.PluginResult?.CopyText;
        if (copyText != null)
        {
            App.API?.CopyToClipboard(copyText, directCopy: false);
        }
    }

    /// <summary>
    /// Copies the selected query result's copy text when the query box has no selection (Ctrl+C).
    /// </summary>
    /// <returns>Whether a result was copied.</returns>
    public bool TryCopySelectedResult()
    {
        var result = Results.SelectedItem?.PluginResult;
        if (result == null)
        {
            return false;
        }

        App.API?.CopyToClipboard(result.CopyText, directCopy: true);
        return true;
    }

    /// <summary>
    /// Replaces the query with the selected result's autocomplete text; with <paramref name="useSubTitle"/> (Shift
    /// held) the subtitle is used instead.
    /// </summary>
    public void AutocompleteQuery(bool useSubTitle = false)
    {
        if (ActiveView != ActiveView.Results)
        {
            return;
        }

        var selected = Results.SelectedItem;
        if (selected == null)
        {
            return;
        }

        string autoCompleteText;
        if (selected.PluginResult is { } result)
        {
            autoCompleteText = result.Title ?? string.Empty;
            if (!string.IsNullOrEmpty(result.AutoCompleteText))
            {
                autoCompleteText = result.AutoCompleteText;
            }
            else if (!string.IsNullOrEmpty(_querySuggestionCompletion))
            {
                autoCompleteText = _querySuggestionCompletion;
            }

            if (useSubTitle)
            {
                autoCompleteText = result.SubTitle ?? string.Empty;
            }
        }
        else if (selected.HistoryItem is { } historyItem)
        {
            autoCompleteText = useSubTitle ? selected.SubTitle : historyItem.Query;
        }
        else
        {
            return;
        }

        ChangeQueryText(autoCompleteText);
    }

    /// <summary>
    /// Ctrl+Backspace on a path query: replaces it with its parent directory.
    /// </summary>
    /// <returns>Whether the query was a path and was changed.</returns>
    public bool TryBackspacePath()
    {
        if (ActiveView != ActiveView.Results || string.IsNullOrEmpty(QueryText))
        {
            return false;
        }

        var query = QueryBuilder.Build(QueryText, QueryText.Trim(), PluginManager.GetNonGlobalPlugins());
        var search = query?.Search;
        if (query == null || string.IsNullOrEmpty(search))
        {
            return false;
        }

        string path;
        if (FilesFolders.IsLocationPathString(search))
        {
            // GetPreviousExistingDirectory does not require trailing '\', otherwise will return empty string
            path = FilesFolders.GetPreviousExistingDirectory(_ => true, search.TrimEnd('\\'));
        }
        else if (!OperatingSystem.IsWindows() && (search.StartsWith('/') || search.StartsWith("~/", StringComparison.Ordinal)))
        {
            var trimmed = search.TrimEnd('/');
            var index = trimmed.LastIndexOf('/');
            path = index >= 0 ? trimmed[..(index + 1)] : string.Empty;
        }
        else
        {
            return false;
        }

        var actionKeyword = string.IsNullOrEmpty(query.ActionKeyword) ? string.Empty : $"{query.ActionKeyword} ";
        ChangeQueryText($"{actionKeyword}{path}");
        return true;
    }

    #endregion

    #region Query

    partial void OnQueryTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowResultsArea));
        OnPropertyChanged(nameof(IsClockPanelVisible));
        UpdateQuerySuggestion();

        if (_ignoredQueryText is not null)
        {
            if (_ignoredQueryText == value)
            {
                _ignoredQueryText = null;
                return;
            }

            _ignoredQueryText = null;
        }

        if (ActiveView == ActiveView.Results)
        {
            UpdateLastQueryActionKeyword(value);
        }

        _ = QueryAsync(_settings.SearchQueryResultsWithDelay);
    }

    /// <summary>
    /// Sets the query text without running a query for it.
    /// </summary>
    private void SetQueryTextSilently(string text)
    {
        if (string.Equals(QueryText, text, StringComparison.Ordinal))
        {
            return;
        }

        _ignoredQueryText = text;
        QueryText = text;
    }

    private void UpdateLastQueryActionKeyword(string queryText)
    {
        var query = QueryBuilder.Build(queryText, queryText.Trim(), PluginManager.GetNonGlobalPlugins());
        _lastQueryActionKeyword = query?.ActionKeyword ?? string.Empty;
    }

    // Port of the WPF QuerySuggestionBoxConverter: the selected result's suggestion (autocomplete text, then title,
    // prefixed with its action keyword) is shown when it starts with the typed query.
    private void UpdateQuerySuggestion()
    {
        _querySuggestionCompletion = string.Empty;
        var queryText = QueryText;
        var result = Results.SelectedItem?.PluginResult;
        if (ActiveView != ActiveView.Results || string.IsNullOrEmpty(queryText) || result == null)
        {
            QuerySuggestionText = string.Empty;
            return;
        }

        var actionKeyword = string.IsNullOrEmpty(result.ActionKeywordAssigned) ? string.Empty : result.ActionKeywordAssigned + " ";
        var suggestion = MatchingSuggestion(actionKeyword, result.QuerySuggestionText, queryText)
            ?? MatchingSuggestion(actionKeyword, result.AutoCompleteText, queryText)
            ?? MatchingSuggestion(actionKeyword, result.Title, queryText);

        if (suggestion == null)
        {
            QuerySuggestionText = string.Empty;
            return;
        }

        // Keep the typed casing so the ghost text lines up with the query box text.
        _querySuggestionCompletion = string.Concat(queryText, suggestion.AsSpan(queryText.Length));
        QuerySuggestionText = _querySuggestionCompletion;
    }

    private static string? MatchingSuggestion(string actionKeyword, string? text, string queryText)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var candidate = actionKeyword + text;
        return candidate.Length > queryText.Length && candidate.StartsWith(queryText, StringComparison.CurrentCultureIgnoreCase)
            ? candidate
            : null;
    }

    private async Task QueryAsync(bool searchDelay = false, bool reselect = true, bool isReQuery = false)
    {
        switch (ActiveView)
        {
            case ActiveView.History:
                QueryHistory(QueryText);
                return;
            case ActiveView.ContextMenu:
                QueryContextMenu();
                return;
            default:
                await QueryResultsAsync(searchDelay, reselect, isReQuery);
                return;
        }
    }

    private async Task QueryResultsAsync(bool searchDelay, bool reselect, bool isReQuery)
    {
        var previousQueryTokenSource = _queryTokenSource;
        previousQueryTokenSource?.Cancel();
        previousQueryTokenSource?.Dispose();
        _queryTokenSource = new CancellationTokenSource();
        var token = _queryTokenSource.Token;
        var queryText = QueryText;
        _currentQueryOriginalText = queryText.Trim();
        var isHomeQuery = string.IsNullOrWhiteSpace(queryText);

        if (isHomeQuery && !_settings.ShowHomePage && !_settings.ShowHistoryResultsForHomePage)
        {
            ClearQueryResults();
            IsQueryRunning = false;
            return;
        }

        if (!_pluginsReady)
        {
            if (isHomeQuery)
            {
                ClearQueryResults();
            }

            IsQueryRunning = false;
            return;
        }

        if (IsQueryRunning)
        {
            StartProgressBarDelay();
        }
        else
        {
            IsQueryRunning = true;
        }

        try
        {
            var query = await ConstructQueryAsync(queryText, _settings.CustomShortcuts, _settings.BuiltinShortcuts);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (query == null)
            {
                ClearQueryResults();
                return;
            }

            query.IsReQuery = isReQuery;
            _currentQueryOriginalText = query.OriginalQuery ?? string.Empty;
            _lastQueryActionKeyword = query.ActionKeyword;

            List<PluginPair> plugins;
            if (query.IsHomeQuery)
            {
                plugins = _settings.ShowHomePage
                    ? PluginManager.ValidPluginsForHomeQuery().Where(p => !p.Metadata.HomeDisabled).ToList()
                    : [];
                PluginIconPath = null;
            }
            else
            {
                plugins = PluginManager.ValidPluginsForQuery(query, dialogJump: false)
                    .Where(p => !p.Metadata.Disabled).ToList();
                PluginIconPath = plugins.Count == 1 && !string.IsNullOrEmpty(query.ActionKeyword)
                    ? plugins[0].Metadata.IcoPath
                    : null;
            }

            // Accumulated results; additions and snapshots happen under one lock so channel order matches growth.
            var allResults = new List<ResultViewModel>();
            if (query.IsHomeQuery && _settings.ShowHistoryResultsForHomePage)
            {
                allResults.AddRange(CreateHistoryResults(_history.LastOpenedHistoryItems, _settings.MaxHistoryResultsToShowForHomePage));
            }

            if (plugins.Count == 0)
            {
                if (allResults.Count == 0)
                {
                    ClearQueryResults();
                }
                else
                {
                    _resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(allResults.ToList(), token, pluginId: null, reselectFirst: reselect));
                }

                return;
            }

            if (allResults.Count > 0)
            {
                _resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(allResults.ToList(), token, pluginId: null, reselectFirst: reselect));
            }

            // Query all plugins in parallel - results shown progressively as each completes
            var tasks = plugins.Select(async plugin =>
            {
                var pluginResults = await QueryPluginAsync(plugin, query, token, searchDelay);
                if (token.IsCancellationRequested) return;

                lock (allResults)
                {
                    allResults.AddRange(pluginResults);

                    // Update UI with current accumulated results (progressive update via channel)
                    if (!token.IsCancellationRequested)
                    {
                        _resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(allResults.ToList(), token, pluginId: null, reselectFirst: reselect));
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Final update after all plugins complete
            if (!token.IsCancellationRequested)
            {
                _resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(allResults.ToList(), token, pluginId: null, reselectFirst: reselect));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Exception(ClassName, "Query error", e); }
        finally { if (!token.IsCancellationRequested) IsQueryRunning = false; }
    }

    private void ClearQueryResults()
    {
        Results.Clear();
        HasResults = false;
        PluginIconPath = null;
    }

    private List<ResultViewModel> CreateResultViewModels(IEnumerable<Result> results, PluginPair plugin)
    {
        var resultViewModels = new List<ResultViewModel>();

        foreach (var result in results)
        {
            resultViewModels.Add(new ResultViewModel
            {
                Title = result.Title ?? string.Empty,
                SubTitle = result.SubTitle ?? string.Empty,
                IconPath = result.IcoPath ?? plugin.Metadata.IcoPath ?? string.Empty,
                Score = AdjustedScore(result, plugin.Metadata),
                PluginResult = result,
                Glyph = result.Glyph,
                TitleHighlightData = result.TitleHighlightData,
            });
        }

        return resultViewModels;
    }

    // The original Result is not mutated because Avalonia does not clone plugin results (a cached Result would be boosted repeatedly).
    private int AdjustedScore(Result result, PluginMetadata metadata)
    {
        var deviationIndex = _topMostRecord.GetTopMostIndex(result);
        if (deviationIndex != -1)
        {
            // A lower deviationIndex (closer to the top) results in a higher score.
            return Result.MaxScore - deviationIndex;
        }

        long score = result.Score + metadata.Priority * 150L;
        if (result.AddSelectedCount)
        {
            score += _userSelectedRecord.GetSelectedCount(result);
        }

        return (int)Math.Min(score, Result.MaxScore);
    }

    private Task<List<ResultViewModel>> QueryPluginAsync(PluginPair plugin, Query query, CancellationToken token, bool searchDelay)
    {
        // Run entirely on thread pool to avoid blocking UI if plugin has synchronous code
        return Task.Run(async () =>
        {
            var resultList = new List<ResultViewModel>();

            try
            {
                if (searchDelay && !query.IsHomeQuery)
                {
                    var delay = plugin.Metadata.SearchDelayTime ?? _settings.SearchDelayTime;
                    if (delay > 0)
                    {
                        await Task.Delay(delay, token);
                    }
                }

                if (token.IsCancellationRequested) return resultList;

                var results = query.IsHomeQuery
                    ? await PluginManager.QueryHomeForPluginAsync(plugin, query, token)
                    : await PluginManager.QueryForPluginAsync(plugin, query, token);
                if (token.IsCancellationRequested || results == null || results.Count == 0) return resultList;

                resultList.AddRange(CreateResultViewModels(results, plugin));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log.Exception(ClassName, $"Plugin {plugin.Metadata.Name} error", e); }

            return resultList;
        }, token);
    }

    private async Task<Query?> ConstructQueryAsync(string queryText, IEnumerable<CustomShortcutModel> customShortcuts,
        IEnumerable<BaseBuiltinShortcutModel> builtInShortcuts)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return QueryBuilder.Build(string.Empty, string.Empty, PluginManager.GetNonGlobalPlugins());
        }

        var queryBuilder = new StringBuilder(queryText);
        var queryBuilderTmp = new StringBuilder(queryText);

        foreach (var shortcut in customShortcuts.OrderByDescending(x => x.Key.Length))
        {
            if (queryBuilder.ToString() == shortcut.Key)
            {
                queryBuilder.Replace(shortcut.Key, shortcut.Expand());
            }

            queryBuilder.Replace('@' + shortcut.Key, shortcut.Expand());
        }

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
                if (!customExpanded.Contains(shortcut.Key, StringComparison.Ordinal))
                {
                    continue;
                }

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
            catch (Exception e)
            {
                Log.Exception(ClassName, $"Error when expanding shortcut {shortcut.Key}", e);
            }
        }

        if (queryChanged)
        {
            ApplyExpandedQueryText(queryBuilderTmp.ToString());
        }
    }

    private void ApplyExpandedQueryText(string expandedQueryText)
    {
        _ignoredQueryText = expandedQueryText;
        QueryText = expandedQueryText;
        RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.CaretAtEnd));
    }

    #endregion

    #region Open results

    [RelayCommand]
    private Task OpenResultAsync() => OpenSelectedResultAsync(SpecialKeyState.Default);

    /// <summary>
    /// Selects <paramref name="result"/> (when given) in the active list and executes it, passing the pressed
    /// modifiers to the plugin as the action context's <see cref="SpecialKeyState"/>.
    /// </summary>
    public async Task OpenResultWithModifiersAsync(ResultViewModel? result, KeyModifiers modifiers)
    {
        if (result != null && !SelectInActiveList(result))
        {
            return;
        }

        await OpenSelectedResultAsync(ToSpecialKeyState(modifiers));
    }

    /// <summary>
    /// Opens the result at <paramref name="index"/> of the active list (open-result modifier + number). The modifier
    /// only selects the result, so it is not passed to the plugin.
    /// </summary>
    public async Task OpenResultAtIndexAsync(int index)
    {
        var list = ActiveResults;
        if (index < 0 || index >= list.Results.Count)
        {
            return;
        }

        list.SelectedIndex = index;
        list.SelectedItem = list.Results[index];
        await OpenSelectedResultAsync(SpecialKeyState.Default);
    }

    public static SpecialKeyState ToSpecialKeyState(KeyModifiers modifiers) => new()
    {
        CtrlPressed = modifiers.HasFlag(KeyModifiers.Control),
        ShiftPressed = modifiers.HasFlag(KeyModifiers.Shift),
        AltPressed = modifiers.HasFlag(KeyModifiers.Alt),
        WinPressed = modifiers.HasFlag(KeyModifiers.Meta),
    };

    private bool SelectInActiveList(ResultViewModel result)
    {
        var list = ActiveResults;
        var index = list.Results.IndexOf(result);
        if (index < 0)
        {
            return false;
        }

        list.SelectedIndex = index;
        list.SelectedItem = result;
        return true;
    }

    private async Task OpenSelectedResultAsync(SpecialKeyState keyState)
    {
        var view = ActiveView;
        var selected = ActiveResults.SelectedItem;
        if (selected == null)
        {
            return;
        }

        if (selected.HistoryItem is { } historyItem)
        {
            await OpenHistoryItemAsync(historyItem, keyState);
            return;
        }

        var result = selected.PluginResult;
        if (result == null) return;

        var queryResultsSelected = view == ActiveView.Results;

        // Recorded before executing: actions that hide the window trigger a home query that reads the history.
        if (queryResultsSelected)
        {
            RecordHistory(QueryText.Trim(), result);
        }

        try
        {
            var hideWindow = await result.ExecuteAsync(new ActionContext { SpecialKeyState = keyState });

            if (queryResultsSelected)
            {
                RecordUserSelected(result);
            }

            if (hideWindow)
            {
                Hide();
            }
            else if (view == ActiveView.ContextMenu && ActiveView == ActiveView.ContextMenu)
            {
                // A context menu action that keeps the window open returns to the result it was opened for.
                ReturnFromContextMenu();
            }
        }
        catch (Exception e) { Log.Exception(ClassName, "Execute error", e); }
    }

    // Query style re-runs the recorded query; last-opened style executes the recorded result again and falls back to
    // the query when the plugin no longer returns it.
    private async Task OpenHistoryItemAsync(LastOpenedHistoryResult historyItem, SpecialKeyState keyState)
    {
        if (_settings.HistoryStyle == HistoryStyle.LastOpened)
        {
            try
            {
                var freshResult = await ResultHelper.PopulateResultsAsync(historyItem.PluginID, historyItem.Query, historyItem.Title, historyItem.SubTitle, historyItem.RecordKey);
                if (freshResult != null)
                {
                    if (await freshResult.ExecuteAsync(new ActionContext { SpecialKeyState = keyState }))
                    {
                        Hide();
                    }

                    return;
                }
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, "Execute history error", e);
                return;
            }
        }

        ChangeQueryText(historyItem.Query);
    }

    #endregion

    #region Context menu

    /// <summary>
    /// Opens the context menu of the selected result, or returns from it when it is open.
    /// </summary>
    [RelayCommand]
    private void ToggleContextMenu()
    {
        if (ActiveView == ActiveView.ContextMenu)
        {
            ReturnFromContextMenu();
        }
        else
        {
            EnterContextMenu();
        }
    }

    /// <summary>
    /// Selects <paramref name="result"/> and opens its context menu; returns from the context menu when it is
    /// already open.
    /// </summary>
    public void ShowContextMenuFor(ResultViewModel result)
    {
        if (ActiveView == ActiveView.ContextMenu)
        {
            ReturnFromContextMenu();
            return;
        }

        if (SelectInActiveList(result))
        {
            EnterContextMenu();
        }
    }

    private void EnterContextMenu()
    {
        var sourceView = ActiveView;
        var source = ActiveResults;
        var selected = source.SelectedItem;
        if (selected == null)
        {
            return;
        }

        List<Result> menuItems;
        try
        {
            if (selected.HistoryItem is { } historyItem)
            {
                menuItems = [ContextMenuDeleteHistory(historyItem), ContextMenuHistoryInfo()];
            }
            else if (sourceView == ActiveView.Results && selected.PluginResult is { } result && !string.IsNullOrEmpty(result.PluginID))
            {
                menuItems = PluginManager.GetContextMenusForPlugin(result)?.ToList() ?? new List<Result>();
                menuItems.Add(ContextMenuTopMost(result));
                if (PluginManager.GetPluginForId(result.PluginID) is { } plugin)
                {
                    menuItems.Add(ContextMenuPluginSettings(result, plugin.Metadata));
                    menuItems.Add(ContextMenuPluginInfo(result, plugin.Metadata));
                }
            }
            else
            {
                return;
            }
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to load context menu", e);
            return;
        }

        _contextMenuSource = new ContextMenuSource(sourceView, QueryText, selected, source.SelectedIndex);
        _contextMenuItems = menuItems;
        if (sourceView == ActiveView.Results)
        {
            _queryTextBeforeLeaveResults = QueryText;
        }

        // The query box filters the menu while it is open.
        ActiveView = ActiveView.ContextMenu;
        SetQueryTextSilently(string.Empty);
        QueryContextMenu();
    }

    private void ReturnFromContextMenu()
    {
        var source = _contextMenuSource;
        _contextMenuSource = null;
        _contextMenuItems = [];

        if (source?.View == ActiveView.History)
        {
            ActiveView = ActiveView.History;
            ContextMenu.Clear();
            SetQueryTextSilently(source.QueryText);
            // Rebuilt because the menu may have deleted history items.
            QueryHistory(source.QueryText);
            RestoreSelection(HistoryView, source);
        }
        else
        {
            ActiveView = ActiveView.Results;
            ContextMenu.Clear();
            SetQueryTextSilently(_queryTextBeforeLeaveResults);
            _queryTextBeforeLeaveResults = string.Empty;
            HasResults = Results.Results.Count > 0;
            if (source != null)
            {
                RestoreSelection(Results, source);
            }
        }

        RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.CaretAtEnd));
    }

    private static void RestoreSelection(ResultsViewModel list, ContextMenuSource source)
    {
        var results = list.Results;
        var index = results.IndexOf(source.Selected);
        if (index < 0 && source.Selected.HistoryItem != null)
        {
            index = results.ToList().FindIndex(r => ReferenceEquals(r.HistoryItem, source.Selected.HistoryItem));
        }

        if (index < 0 && source.SelectedIndex >= 0 && results.Count > 0)
        {
            index = Math.Min(source.SelectedIndex, results.Count - 1);
        }

        if (index >= 0)
        {
            list.SelectedIndex = index;
            list.SelectedItem = results[index];
        }
    }

    private void QueryContextMenu()
    {
        var query = QueryText.ToLowerInvariant().Trim();
        var items = new List<ResultViewModel>();

        foreach (var result in _contextMenuItems)
        {
            var score = result.Score;
            if (!string.IsNullOrEmpty(query))
            {
                var match = App.API.FuzzySearch(query, result.Title ?? string.Empty);
                if (!match.IsSearchPrecisionScoreMet())
                {
                    match = App.API.FuzzySearch(query, result.SubTitle ?? string.Empty);
                }

                if (!match.IsSearchPrecisionScoreMet())
                {
                    continue;
                }

                score = match.Score;
            }

            items.Add(new ResultViewModel
            {
                Title = result.Title ?? string.Empty,
                SubTitle = result.SubTitle ?? string.Empty,
                IconPath = string.IsNullOrEmpty(result.IcoPathAbsolute) ? result.IcoPath ?? string.Empty : result.IcoPathAbsolute,
                Score = score,
                PluginResult = result,
                Glyph = result.Glyph
            });
        }

        ContextMenu.ReplaceResults(items);
    }

    private Result ContextMenuTopMost(Result result)
    {
        if (_topMostRecord.IsTopMost(result))
        {
            return new Result
            {
                Title = Translate("cancelTopMostInThisQuery"),
                IcoPath = Path.Combine(Constant.ProgramDirectory, "Images", "down.png"),
                PluginDirectory = Constant.ProgramDirectory,
                Action = _ =>
                {
                    _topMostRecord.Remove(result);
                    _topMostRecord.Save();
                    App.API?.ShowMsg(Translate("success"));
                    ReQuery();
                    return false;
                },
                Glyph = new GlyphInfo(FontFamily: SegoeFluentIcons, Glyph: "\uE74B"),
                OriginQuery = result.OriginQuery
            };
        }

        return new Result
        {
            Title = Translate("setAsTopMostInThisQuery"),
            IcoPath = Path.Combine(Constant.ProgramDirectory, "Images", "up.png"),
            PluginDirectory = Constant.ProgramDirectory,
            Action = _ =>
            {
                _topMostRecord.AddOrUpdate(result);
                _topMostRecord.Save();
                App.API?.ShowMsg(Translate("success"));
                ReQuery();
                return false;
            },
            Glyph = new GlyphInfo(FontFamily: SegoeFluentIcons, Glyph: "\uE74A"),
            OriginQuery = result.OriginQuery
        };
    }

    private static Result ContextMenuPluginSettings(Result result, PluginMetadata metadata)
    {
        var pluginId = metadata.ID;
        return new Result
        {
            Title = FormatTranslation("pluginSettingsWindowTitle", metadata.Name),
            IcoPath = Constant.SettingsIcon,
            Glyph = new GlyphInfo(FontFamily: SegoeFluentIcons, Glyph: "\uE713"),
            PluginDirectory = Constant.ProgramDirectory,
            Action = _ =>
            {
                Dispatcher.UIThread.Post(() => SettingsWindow.Open(pluginId));
                return true;
            },
            OriginQuery = result.OriginQuery
        };
    }

    private static Result ContextMenuPluginInfo(Result result, PluginMetadata metadata)
    {
        var website = metadata.Website;
        return new Result
        {
            Title = $"{Translate("plugin")}: {metadata.Name}",
            SubTitle = $"{Translate("author")} {metadata.Author}",
            IcoPath = metadata.IcoPath,
            PluginDirectory = metadata.PluginDirectory,
            Action = _ =>
            {
                if (!string.IsNullOrEmpty(website))
                {
                    App.API?.OpenUrl(website);
                }

                return true;
            },
            OriginQuery = result.OriginQuery
        };
    }

    private Result ContextMenuDeleteHistory(LastOpenedHistoryResult historyItem)
    {
        return new Result
        {
            Title = Translate("delete"),
            IcoPath = Constant.DeleteIcon,
            Glyph = new GlyphInfo(FontFamily: SegoeFluentIcons, Glyph: "\uE74D"),
            PluginDirectory = Constant.ProgramDirectory,
            Action = _context =>
            {
                var sourceView = _contextMenuSource?.View;
                var removeAllMatchingResults = _settings.HistoryStyle == HistoryStyle.LastOpened;
                if (_history.Remove(historyItem, removeAllMatchingResults) > 0)
                {
                    SaveHistoryInBackground();
                }

                ReturnFromContextMenu();

                // Home-page history is part of the regular result list, so refresh it after deletion.
                // The dedicated history view is rebuilt while returning from the context menu.
                if (sourceView == ActiveView.Results)
                {
                    _ = QueryAsync(isReQuery: true);
                }

                return false;
            }
        };
    }

    // Labels the history context menu with the current history style.
    private Result ContextMenuHistoryInfo()
    {
        return new Result
        {
            Title = Translate(_settings.HistoryStyle == HistoryStyle.Query ? "queryHistory" : "executedHistory"),
            IcoPath = Constant.HistoryIcon,
            Glyph = HistoryGlyph,
            PluginDirectory = Constant.ProgramDirectory,
            Action = _ => false
        };
    }

    #endregion

    private static string Translate(string key) => App.API?.GetTranslation(key) ?? key;

    private static string FormatTranslation(string key, object? argument)
    {
        var template = Translate(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, argument);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}

/// <summary>
/// Represents a batch of results from a plugin for UI update.
/// Used for channel-based debouncing.
/// </summary>
internal readonly struct ResultsForUpdate
{
    public IReadOnlyList<ResultViewModel> Results { get; }
    public CancellationToken Token { get; }
    public string? PluginId { get; }
    public bool ReselectFirst { get; }
    public bool IsPluginUpdate => !string.IsNullOrEmpty(PluginId);

    public ResultsForUpdate(IReadOnlyList<ResultViewModel> results, CancellationToken token, string? pluginId = null, bool reselectFirst = true)
    {
        Results = results;
        Token = token;
        PluginId = pluginId;
        ReselectFirst = reselectFirst;
    }
}
