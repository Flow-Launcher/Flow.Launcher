using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Resource;
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

    public event Action? HideRequested;
    public event Action? ShowRequested;

    [ObservableProperty]
    private bool _mainWindowVisibility = true;

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
    /// </summary>
    public bool ShowResultsArea => HasResults || IsContextMenuViewActive;

    public Settings Settings => _settings;

    public MainViewModel(Settings settings)
    {
        _settings = settings;
        _results = new ResultsViewModel(settings);
        _contextMenu = new ResultsViewModel(settings);
    }

    partial void OnActiveViewChanged(ActiveView value)
    {
        OnPropertyChanged(nameof(IsResultsViewActive));
        OnPropertyChanged(nameof(IsContextMenuViewActive));
        OnPropertyChanged(nameof(ShowResultsArea));
    }

    partial void OnHasResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowResultsArea));
    }

    public void OnPluginsReady()
    {
        _pluginsReady = true;
        Log.Info(ClassName, "Plugins ready");
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
    /// Show the main window.
    /// </summary>
    public void Show()
    {
        MainWindowVisibility = true;
        ShowRequested?.Invoke();
        Log.Info(ClassName, "Show requested");
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
    /// Go back from context menu to results view.
    /// </summary>
    [RelayCommand]
    private void BackToResults()
    {
        ActiveView = ActiveView.Results;
        ContextMenu.Clear();
    }

    partial void OnQueryTextChanged(string value) => _ = QueryAsync();

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
            if (query == null) { HasResults = false; return; }

            var plugins = PluginManager.ValidPluginsForQuery(query, dialogJump: false)
                .Where(p => !p.Metadata.Disabled).ToList();

            if (plugins.Count == 0) { HasResults = false; return; }

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

                // Update UI with current accumulated results (progressive update)
                if (!token.IsCancellationRequested)
                {
                    await UpdateResultsOnUIThread(allResults, token);
                }
            });

            await Task.WhenAll(tasks);

            // Final update after all plugins complete
            if (!token.IsCancellationRequested)
            {
                await UpdateResultsOnUIThread(allResults, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Exception(ClassName, "Query error", e); }
        finally { if (!token.IsCancellationRequested) IsQueryRunning = false; }
    }

    private async Task UpdateResultsOnUIThread(ConcurrentBag<ResultViewModel> allResults, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var sortedResults = allResults
            .OrderByDescending(r => r.Score)
            .Take(_settings.MaxResultsToShow)
            .ToList();

        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            Results.ReplaceResults(sortedResults);
            HasResults = Results.Results.Count > 0;
        });
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
                        PluginResult = r
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
                PluginResult = r
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
