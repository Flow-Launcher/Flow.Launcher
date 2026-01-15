using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.ViewModel;

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

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private bool _isQueryRunning;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private ResultsViewModel _results;

    public Settings Settings => _settings;

    public MainViewModel(Settings settings)
    {
        _settings = settings;
        _results = new ResultsViewModel(settings);
    }

    public void OnPluginsReady()
    {
        _pluginsReady = true;
        Log.Info(ClassName, "Plugins ready");
        if (!string.IsNullOrWhiteSpace(QueryText))
            _ = QueryAsync();
    }

    public void RequestHide() => HideRequested?.Invoke();

    partial void OnQueryTextChanged(string value) => _ = QueryAsync();

    private async Task QueryAsync()
    {
        _queryTokenSource?.Cancel();
        _queryTokenSource = new CancellationTokenSource();
        var token = _queryTokenSource.Token;
        var queryText = QueryText.Trim();

        if (string.IsNullOrWhiteSpace(queryText) || !_pluginsReady)
        {
            Results.Clear();
            HasResults = false;
            IsQueryRunning = false;
            return;
        }

        IsQueryRunning = true;

        try
        {
            var query = QueryBuilder.Build(queryText, PluginManager.NonGlobalPlugins);
            if (query == null) { Results.Clear(); HasResults = false; return; }

            var plugins = PluginManager.ValidPluginsForQuery(query, dialogJump: false)
                .Where(p => !p.Metadata.Disabled).ToList();

            if (plugins.Count == 0) { Results.Clear(); HasResults = false; return; }

            Results.Clear();

            var tasks = plugins.Select(p => QueryPluginAsync(p, query, token));
            await Task.WhenAll(tasks);

            if (!token.IsCancellationRequested)
                HasResults = Results.Results.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Exception(ClassName, "Query error", e); }
        finally { if (!token.IsCancellationRequested) IsQueryRunning = false; }
    }

    private async Task QueryPluginAsync(PluginPair plugin, Query query, CancellationToken token)
    {
        try
        {
            var delay = plugin.Metadata.SearchDelayTime ?? _settings.SearchDelayTime;
            if (delay > 0) await Task.Delay(delay, token);
            if (token.IsCancellationRequested) return;

            var results = await PluginManager.QueryForPluginAsync(plugin, query, token);
            if (token.IsCancellationRequested || results == null || results.Count == 0) return;

            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var r in results.OrderByDescending(r => r.Score).Take(_settings.MaxResultsToShow))
                {
                    if (token.IsCancellationRequested) return;
                    Results.AddResult(new ResultViewModel
                    {
                        Title = r.Title ?? "",
                        SubTitle = r.SubTitle ?? "",
                        IconPath = r.IcoPath ?? plugin.Metadata.IcoPath ?? "",
                        PluginResult = r
                    });
                }
                HasResults = Results.Results.Count > 0;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Exception(ClassName, $"Plugin {plugin.Metadata.Name} error", e); }
    }

    [RelayCommand]
    private void Esc() { QueryText = ""; HideRequested?.Invoke(); }

    [RelayCommand]
    private async Task OpenResultAsync()
    {
        var result = Results.SelectedItem?.PluginResult;
        if (result == null) return;
        try
        {
            if (await result.ExecuteAsync(new ActionContext { SpecialKeyState = SpecialKeyState.Default }))
                HideRequested?.Invoke();
        }
        catch (Exception e) { Log.Exception(ClassName, "Execute error", e); }
    }

    [RelayCommand] private void SelectNextItem() => Results.SelectNextItem();
    [RelayCommand] private void SelectPrevItem() => Results.SelectPrevItem();
}
