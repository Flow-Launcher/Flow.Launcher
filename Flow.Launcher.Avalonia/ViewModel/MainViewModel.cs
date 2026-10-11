using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Storage;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

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
/// MainViewModel for Avalonia - minimal implementation for plugin queries.
/// </summary>
public partial class MainViewModel : ObservableObject, IResultUpdateRegister
{
    private static readonly string ClassName = nameof(MainViewModel);
    private const int ProgressBarDelayMilliseconds = 200;
    private readonly Settings _settings;
    private readonly FlowLauncherJsonStorage<History> _historyItemsStorage;
    private readonly History _history;
    private CancellationTokenSource? _queryTokenSource;
    private string? _ignoredQueryText;
    private string _queryTextBeforeHistory = string.Empty;
    private bool _pluginsReady;
    private int _lastHistoryIndex = 1;
    private string _currentQueryOriginalText = string.Empty;
    private string _lastQueryActionKeyword = string.Empty;
    private CancellationTokenSource? _progressBarDelayTokenSource;

    // Channel-based debouncing for result updates (matches WPF approach)
    private readonly Channel<ResultsForUpdate> _resultsUpdateChannel;
    private readonly ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
    private readonly Task _resultsViewUpdateTask;

    public event Action? HideRequested;
    public event Action<QueryTextFocusRequest>? QueryTextFocusRequested;

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
    /// Based on whether we have a non-empty query - NOT on collection count to prevent flickering.
    /// </summary>
    public bool ShowResultsArea => ActiveView == ActiveView.History || !string.IsNullOrWhiteSpace(QueryText) || ContextMenu.Results.Count > 0;

    public Settings Settings => _settings;

    public bool LastQuerySelected { get; set; }

    public MainViewModel(Settings settings)
    {
        _settings = settings;
        _historyItemsStorage = new FlowLauncherJsonStorage<History>();
        _history = _historyItemsStorage.Load();
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
        
        // Subscribe to context menu collection changes for ShowResultsArea (context menu still uses count)
        ((System.Collections.Specialized.INotifyCollectionChanged)_contextMenu.Results).CollectionChanged += (s, e) => OnPropertyChanged(nameof(ShowResultsArea));
    }

    /// <summary>
    /// Background task that processes result updates with debouncing.
    /// Waits 20ms to batch multiple plugin completions into a single UI update.
    /// </summary>
    private async Task ProcessResultUpdatesAsync()
    {
        var channelReader = _resultsUpdateChannel.Reader;

        while (await channelReader.WaitToReadAsync())
        {
            try
            {
                // Wait 20ms to allow multiple plugin results to arrive
                await Task.Delay(20);

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

        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (update.Token.IsCancellationRequested) return;
            if (update.IsPluginUpdate)
            {
                Results.ReplaceResultsForPlugin(update.PluginId!, sortedResults);
            }
            else
            {
                Results.ReplaceResults(sortedResults);
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

        PreviewSelectedItem = value switch
        {
            ActiveView.Results => Results.SelectedItem,
            ActiveView.ContextMenu => ContextMenu.SelectedItem,
            ActiveView.History => HistoryView.SelectedItem,
            _ => null,
        };
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

    [RelayCommand]
    public void TogglePreview()
    {
        IsPreviewOn = !IsPreviewOn;
    }

    public void OnPluginsReady()
    {
        _pluginsReady = true;
        MainWindowVisibility = !_settings.HideOnStartup;
        Log.Info(ClassName, MainWindowVisibility ? "Plugins ready - window shown" : "Plugins ready - window kept hidden");
        if (!string.IsNullOrWhiteSpace(QueryText))
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
                !string.Equals(e.Query.OriginalQuery, _currentQueryOriginalText, StringComparison.Ordinal))
            {
                return;
            }

            var token = e.Token == default ? _queryTokenSource?.Token ?? CancellationToken.None : e.Token;
            if (token.IsCancellationRequested)
            {
                return;
            }

            var results = CreateResultViewModels(e.Results, pair);
            if (!_resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(results, token, pair.Metadata.ID)))
            {
                Log.Error(ClassName, "Unable to add item to Result Update Queue");
            }
        };
    }

    public void RequestHide() => HideRequested?.Invoke();

    public void RequestQueryTextFocus(QueryTextFocusRequest request)
    {
        QueryTextFocusRequested?.Invoke(request);
    }

    public void ReQuery(bool reselect = true) => _ = QueryAsync();

    public void BackToQueryResults() => BackToResults();

    public void Save() => _historyItemsStorage.Save();

    [RelayCommand]
    private void ReverseHistory()
    {
        var historyItems = _history.LastOpenedHistoryItems;
        if (historyItems.Count == 0)
        {
            return;
        }

        ApplyHistoryQueryText(historyItems[^_lastHistoryIndex].Query);
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

        ApplyHistoryQueryText(historyItems[^_lastHistoryIndex].Query);
        if (_lastHistoryIndex > 1)
        {
            _lastHistoryIndex--;
        }
    }

    [RelayCommand]
    private void LoadHistory()
    {
        if (ActiveView == ActiveView.Results)
        {
            _queryTextBeforeHistory = QueryText;
            ActiveView = ActiveView.History;
            QueryText = string.Empty;
            _ = QueryAsync();

            if (HistoryView.Results.Count > 0)
            {
                HistoryView.SelectedIndex = 0;
                HistoryView.SelectedItem = HistoryView.Results[0];
            }

            return;
        }

        if (ActiveView == ActiveView.History)
        {
            BackToResultsFromHistory();
            return;
        }

        BackToResults();
    }

    /// <summary>
    /// Toggle the main window visibility. Called by global hotkey.
    /// </summary>
    public void ToggleFlowLauncher()
    {
        Log.Info(ClassName, $"ToggleFlowLauncher called, currently visible: {MainWindowVisibility}");
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
        PrepareLastQueryModeBeforeHide();
        HideRequested?.Invoke();
        MainWindowVisibility = false;
        ResetTransientViewState();
        ApplyLastQueryModeForHide();
        Log.Info(ClassName, "Hide requested");
    }

    /// <summary>
    /// Show the main window.
    /// </summary>
    public void Show()
    {
        ResetTransientViewState();
        var focusMode = ApplyLastQueryModeForShow();
        RequestQueryTextFocus(new QueryTextFocusRequest(true, true, focusMode));
        MainWindowVisibility = true;
        Log.Info(ClassName, "Show requested");
    }

    private void ResetTransientViewState()
    {
        _lastHistoryIndex = 1;
        _queryTextBeforeHistory = string.Empty;
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

    /// <summary>
    /// Show the main window with an injected query.
    /// </summary>
    public void ShowWithInjectedQuery(string queryText)
    {
        ResetTransientViewState();
        QueryText = queryText;
        RequestQueryTextFocus(new QueryTextFocusRequest(true, true, QueryTextFocusMode.CaretAtEnd));
        MainWindowVisibility = true;
        Log.Info(ClassName, "Show with injected query requested");
    }

    /// <summary>
    /// Go back from context menu to results view.
    /// </summary>
    [RelayCommand]
    private void BackToResults()
    {
        ActiveView = ActiveView.Results;
        ContextMenu.Clear();
    }

    partial void OnQueryTextChanged(string value)
    {
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

        // Notify ShowResultsArea when query text changes (it depends on QueryText)
        OnPropertyChanged(nameof(ShowResultsArea));
        _ = QueryAsync(_settings.SearchQueryResultsWithDelay);
    }

    private void UpdateLastQueryActionKeyword(string queryText)
    {
        var query = QueryBuilder.Build(queryText, queryText.Trim(), PluginManager.GetNonGlobalPlugins());
        _lastQueryActionKeyword = query?.ActionKeyword ?? string.Empty;
    }

    private async Task QueryAsync(bool searchDelay = false)
    {
        var previousQueryTokenSource = _queryTokenSource;
        previousQueryTokenSource?.Cancel();
        previousQueryTokenSource?.Dispose();
        _queryTokenSource = new CancellationTokenSource();
        var token = _queryTokenSource.Token;
        var queryText = QueryText.Trim();
        _currentQueryOriginalText = queryText;

        if (ActiveView == ActiveView.History)
        {
            QueryHistory(queryText);
            IsQueryRunning = false;
            return;
        }

        // Only clear results when query is empty
        if (string.IsNullOrWhiteSpace(queryText))
        {
            Results.Clear();
            HasResults = false;
            IsQueryRunning = false;
            return;
        }

        if (!_pluginsReady)
        {
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
            var query = await ConstructQueryAsync(QueryText, _settings.CustomShortcuts, _settings.BuiltinShortcuts);
            if (query == null)
            {
                Results.Clear();
                HasResults = false;
                return;
            }

            _currentQueryOriginalText = query.OriginalQuery;
            _lastQueryActionKeyword = query.ActionKeyword;

            var plugins = PluginManager.ValidPluginsForQuery(query, dialogJump: false)
                .Where(p => !p.Metadata.Disabled).ToList();

            if (plugins.Count == 0)
            {
                Results.Clear();
                HasResults = false;
                return;
            }

            // Use a thread-safe collection to accumulate results from all plugins
            var allResults = new ConcurrentBag<ResultViewModel>();

            // Query all plugins in parallel - results shown progressively as each completes
            var tasks = plugins.Select(async plugin =>
            {
                var pluginResults = await QueryPluginAsync(plugin, query, token, searchDelay);
                if (token.IsCancellationRequested) return;

                // Add results to the bag
                foreach (var r in pluginResults)
                {
                    allResults.Add(r);
                }

                // Update UI with current accumulated results (progressive update via channel)
                if (!token.IsCancellationRequested)
                {
                    _resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(allResults.ToList(), token));
                }
            });

            await Task.WhenAll(tasks);

            // Final update after all plugins complete
            if (!token.IsCancellationRequested)
            {
                _resultsUpdateChannelWriter.TryWrite(new ResultsForUpdate(allResults.ToList(), token));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Exception(ClassName, "Query error", e); }
        finally { if (!token.IsCancellationRequested) IsQueryRunning = false; }
    }

    private void QueryHistory(string queryText)
    {
        var normalizedQuery = queryText.ToLowerInvariant().Trim();
        var historyItems = _history.LastOpenedHistoryItems.OrderByDescending(item => item.ExecutedDateTime).ToList();
        var historyResults = historyItems
            .Select((item, index) => CreateHistoryResult(item, historyItems.Count - index))
            .ToList();

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

    private static ResultViewModel CreateHistoryResult(LastOpenedHistoryResult item, int score)
    {
        return new ResultViewModel
        {
            Title = item.Title,
            SubTitle = item.SubTitle,
            Score = score,
            HistoryItem = item,
        };
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
                Score = result.Score,
                PluginResult = result,
                Glyph = result.Glyph,
                TitleHighlightData = result.TitleHighlightData,
            });
        }

        return resultViewModels;
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

                var results = await PluginManager.QueryForPluginAsync(plugin, query, token);
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

    private void ApplyHistoryQueryText(string historyQueryText)
    {
        QueryText = historyQueryText;
        RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.CaretAtEnd));
    }

    private void BackToResultsFromHistory(bool restoreQueryText = true)
    {
        ActiveView = ActiveView.Results;
        HistoryView.Clear();
        HasResults = Results.Results.Count > 0;

        if (!restoreQueryText)
        {
            return;
        }

        var queryToRestore = _queryTextBeforeHistory;
        _queryTextBeforeHistory = string.Empty;
        ApplyHistoryQueryText(queryToRestore);
    }

    private async Task RecordHistoryAsync(string executedQueryText, Result result)
    {
        _history.Add(executedQueryText, result);
        await _historyItemsStorage.SaveAsync();
        _lastHistoryIndex = 1;
    }

    [RelayCommand]
    private void Esc()
    {
        // If in context menu/history, go back to results; otherwise hide window
        if (ActiveView == ActiveView.ContextMenu)
        {
            BackToResults();
        }
        else if (ActiveView == ActiveView.History)
        {
            BackToResultsFromHistory();
        }
        else
        {
            Hide();
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        Hide();
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        });
    }

    [RelayCommand]
    private async Task OpenResultAsync()
    {
        var queryResultsSelected = ActiveView == ActiveView.Results;
        var executedQueryText = QueryText.Trim();
        Result? result;
        if (ActiveView == ActiveView.ContextMenu)
        {
            result = ContextMenu.SelectedItem?.PluginResult;
        }
        else if (ActiveView == ActiveView.History)
        {
            await OpenHistoryResultAsync();
            return;
        }
        else
        {
            result = Results.SelectedItem?.PluginResult;
        }

        if (result == null) return;

        try
        {
            if (await result.ExecuteAsync(new ActionContext { SpecialKeyState = SpecialKeyState.Default }))
            {
                if (queryResultsSelected)
                {
                    await RecordHistoryAsync(executedQueryText, result);
                }

                Hide();
            }
            else if (ActiveView == ActiveView.ContextMenu)
            {
                // If context menu action didn't hide, go back to results
                BackToResults();
            }
            else if (queryResultsSelected)
            {
                await RecordHistoryAsync(executedQueryText, result);
            }
        }
        catch (Exception e) { Log.Exception(ClassName, "Execute error", e); }
    }

    private async Task OpenHistoryResultAsync()
    {
        var historyItem = HistoryView.SelectedItem?.HistoryItem;
        if (historyItem == null)
        {
            return;
        }

        try
        {
            var freshResult = await Helper.ResultHelper.PopulateResultsAsync(historyItem.PluginID, historyItem.Query, historyItem.Title, historyItem.SubTitle, historyItem.RecordKey);
            if (freshResult != null)
            {
                var shouldHide = await freshResult.ExecuteAsync(new ActionContext { SpecialKeyState = SpecialKeyState.Default });
                if (shouldHide)
                {
                    Hide();
                }

                return;
            }

            BackToResultsFromHistory(false);
            ApplyHistoryQueryText(historyItem.Query);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Execute history error", e);
        }
    }

    /// <summary>
    /// Load context menu for the currently selected result.
    /// </summary>
    [RelayCommand]
    private void LoadContextMenu()
    {
        if (ActiveView == ActiveView.History)
        {
            return;
        }

        var selectedResult = Results.SelectedItem?.PluginResult;
        if (selectedResult == null) return;

        try
        {
            var contextMenuResults = PluginManager.GetContextMenusForPlugin(selectedResult);
            if (contextMenuResults == null || contextMenuResults.Count == 0) return;

            var contextMenuItems = contextMenuResults.Select(r => new ResultViewModel
            {
                Title = r.Title ?? "",
                SubTitle = r.SubTitle ?? "",
                IconPath = r.IcoPath ?? "",
                Score = r.Score,
                PluginResult = r,
                Glyph = r.Glyph
            }).ToList();

            ContextMenu.ReplaceResults(contextMenuItems);
            ActiveView = ActiveView.ContextMenu;
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to load context menu", e);
        }
    }

    [RelayCommand]
    private void SelectNextItem()
    {
        if (ActiveView == ActiveView.ContextMenu)
            ContextMenu.SelectNextItem();
        else if (ActiveView == ActiveView.History)
            HistoryView.SelectNextItem();
        else
            Results.SelectNextItem();
    }

    [RelayCommand]
    private void SelectPrevItem()
    {
        if (ActiveView == ActiveView.ContextMenu)
            ContextMenu.SelectPrevItem();
        else if (ActiveView == ActiveView.History)
            HistoryView.SelectPrevItem();
        else
            Results.SelectPrevItem();
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
    public bool IsPluginUpdate => !string.IsNullOrEmpty(PluginId);

    public ResultsForUpdate(IReadOnlyList<ResultViewModel> results, CancellationToken token, string? pluginId = null)
    {
        Results = results;
        Token = token;
        PluginId = pluginId;
    }
}
