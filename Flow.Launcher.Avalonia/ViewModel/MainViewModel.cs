using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core.Plugin;
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
    ContextMenu
}

/// <summary>
/// MainViewModel for Avalonia - minimal implementation for plugin queries.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private static readonly string ClassName = nameof(MainViewModel);
    private readonly Settings _settings;
    private CancellationTokenSource? _queryTokenSource;
    private bool _pluginsReady;

    // Channel-based debouncing for result updates (matches WPF approach)
    private readonly Channel<ResultsForUpdate> _resultsUpdateChannel;
    private readonly ChannelWriter<ResultsForUpdate> _resultsUpdateChannelWriter;
    private readonly Task _resultsViewUpdateTask;

    public event Action? HideRequested;
    public event Action? ShowRequested;

    [ObservableProperty]
    private bool _mainWindowVisibility = false;

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private bool _isQueryRunning;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private ResultsViewModel _results;

    [ObservableProperty]
    private ResultsViewModel _contextMenu;

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
    /// Whether to show the results/context menu area (separator + list).
    /// Based on whether we have a non-empty query - NOT on collection count to prevent flickering.
    /// </summary>
    public bool ShowResultsArea => !string.IsNullOrWhiteSpace(QueryText) || ContextMenu.Results.Count > 0;

    public Settings Settings => _settings;

    public MainViewModel(Settings settings)
    {
        _settings = settings;
        _results = new ResultsViewModel(settings);
        _contextMenu = new ResultsViewModel(settings);

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
            // Wait 20ms to allow multiple plugin results to arrive
            await Task.Delay(20);

            // Get the latest snapshot from the channel (discard intermediate ones)
            ResultsForUpdate? latestUpdate = null;

            while (channelReader.TryRead(out var update))
            {
                if (!update.Token.IsCancellationRequested)
                {
                    latestUpdate = update;
                }
            }

            // Apply batched update on UI thread
            if (latestUpdate.HasValue && !latestUpdate.Value.Token.IsCancellationRequested)
            {
                var update = latestUpdate.Value;
                var sortedResults = update.Results
                    .OrderByDescending(r => r.Score)
                    .ToList();

                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (update.Token.IsCancellationRequested) return;
                    Results.ReplaceResults(sortedResults);
                    HasResults = Results.Results.Count > 0;
                });
            }
        }
    }

    partial void OnActiveViewChanged(ActiveView value)
    {
        OnPropertyChanged(nameof(IsResultsViewActive));
        OnPropertyChanged(nameof(IsContextMenuViewActive));
        OnPropertyChanged(nameof(ShowResultsArea));
        
        PreviewSelectedItem = value == ActiveView.Results ? Results.SelectedItem : ContextMenu.SelectedItem;
    }

    partial void OnIsQueryRunningChanged(bool value)
    {
        // ShowResultsArea no longer depends on IsQueryRunning - it uses QueryText instead
    }

    [RelayCommand]
    public void TogglePreview()
    {
        IsPreviewOn = !IsPreviewOn;
    }

    public void OnPluginsReady()
    {
        _pluginsReady = true;
        MainWindowVisibility = true;
        Log.Info(ClassName, "Plugins ready - window shown");
        if (!string.IsNullOrWhiteSpace(QueryText))
            _ = QueryAsync();
    }

    public void RequestHide() => HideRequested?.Invoke();

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
        MainWindowVisibility = false;
        QueryText = "";
        ActiveView = ActiveView.Results;
        ContextMenu.Clear();
        HideRequested?.Invoke();
        Log.Info(ClassName, "Hide requested");
    }

    /// <summary>
    /// Show the main window.
    /// </summary>
    public void Show()
    {
        MainWindowVisibility = true;
        QueryText = "";
        ActiveView = ActiveView.Results;
        ContextMenu.Clear();
        ShowRequested?.Invoke();
        Log.Info(ClassName, "Show requested");
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
        // Notify ShowResultsArea when query text changes (it depends on QueryText)
        OnPropertyChanged(nameof(ShowResultsArea));
        _ = QueryAsync();
    }

    private async Task QueryAsync()
    {
        _queryTokenSource?.Cancel();
        _queryTokenSource = new CancellationTokenSource();
        var token = _queryTokenSource.Token;
        var queryText = QueryText.Trim();

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

        IsQueryRunning = true;

        try
        {
            var query = QueryBuilder.Build(queryText, PluginManager.NonGlobalPlugins);
            if (query == null)
            {
                Results.Clear();
                HasResults = false;
                return;
            }

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
                var pluginResults = await QueryPluginAsync(plugin, query, token);
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

    private Task<List<ResultViewModel>> QueryPluginAsync(PluginPair plugin, Query query, CancellationToken token)
    {
        // Run entirely on thread pool to avoid blocking UI if plugin has synchronous code
        return Task.Run(async () =>
        {
            var resultList = new List<ResultViewModel>();

            try
            {
                var delay = plugin.Metadata.SearchDelayTime ?? _settings.SearchDelayTime;
                if (delay > 0) await Task.Delay(delay, token);
                if (token.IsCancellationRequested) return resultList;

                var results = await PluginManager.QueryForPluginAsync(plugin, query, token);
                if (token.IsCancellationRequested || results == null || results.Count == 0) return resultList;

                foreach (var r in results)
                {
                    resultList.Add(new ResultViewModel
                    {
                        Title = r.Title ?? "",
                        SubTitle = r.SubTitle ?? "",
                        IconPath = r.IcoPath ?? plugin.Metadata.IcoPath ?? "",
                        Score = r.Score,
                        PluginResult = r,
                        Glyph = r.Glyph,
                        TitleHighlightData = r.TitleHighlightData
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log.Exception(ClassName, $"Plugin {plugin.Metadata.Name} error", e); }

            return resultList;
        }, token);
    }

    [RelayCommand]
    private void Esc()
    {
        // If in context menu, go back to results; otherwise hide window
        if (ActiveView == ActiveView.ContextMenu)
        {
            BackToResults();
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
        Result? result;
        if (ActiveView == ActiveView.ContextMenu)
        {
            result = ContextMenu.SelectedItem?.PluginResult;
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
                Hide();
            }
            else if (ActiveView == ActiveView.ContextMenu)
            {
                // If context menu action didn't hide, go back to results
                BackToResults();
            }
        }
        catch (Exception e) { Log.Exception(ClassName, "Execute error", e); }
    }

    /// <summary>
    /// Load context menu for the currently selected result.
    /// </summary>
    [RelayCommand]
    private void LoadContextMenu()
    {
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
        else
            Results.SelectNextItem();
    }

    [RelayCommand]
    private void SelectPrevItem()
    {
        if (ActiveView == ActiveView.ContextMenu)
            ContextMenu.SelectPrevItem();
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

    public ResultsForUpdate(IReadOnlyList<ResultViewModel> results, CancellationToken token)
    {
        Results = results;
        Token = token;
    }
}
