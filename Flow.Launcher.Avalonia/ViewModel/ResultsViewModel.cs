using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// ViewModel for the results list.
/// Uses DynamicData SourceList for automatic sorting by score.
/// </summary>
public partial class ResultsViewModel : ObservableObject, IDisposable
{
    private readonly Settings _settings;
    private readonly ObservableCollection<ResultViewModel> _results = new();
    private readonly ReadOnlyObservableCollection<ResultViewModel> _readOnlyResults;

    [ObservableProperty]
    private ResultViewModel? _selectedItem;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Sorted results collection bound to the UI.
    /// Automatically sorted by Score descending.
    /// </summary>
    public ReadOnlyObservableCollection<ResultViewModel> Results => _readOnlyResults;

    public Settings Settings => _settings;

    public int MaxHeight => (int)(_settings.MaxResultsToShow * _settings.ItemHeightSize);

    public ResultsViewModel(Settings settings)
    {
        _settings = settings;
        _readOnlyResults = new ReadOnlyObservableCollection<ResultViewModel>(_results);
        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    // The result list height follows the max-results setting live (Ctrl+= / Ctrl+-, window resize, settings page).
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.MaxResultsToShow) or nameof(Settings.ItemHeightSize))
        {
            OnPropertyChanged(nameof(MaxHeight));
        }
    }

    /// <summary>
    /// Replace all results with new ones using atomic Edit to prevent flickering.
    /// Edit batches changes and fires only one notification at the end.
    /// </summary>
    public void ReplaceResults(IEnumerable<ResultViewModel> newResults, bool reselect = true)
    {
        var previousSelection = SelectedItem;
        var resultsList = newResults
            .OrderByDescending(r => r.Score)
            .ToList();
        foreach (var r in resultsList)
        {
            r.Settings = _settings;
        }

        // Update highlight data and score for items that will be kept.
        var existingItems = new Dictionary<(string PluginId, string RecordKey, string Query, string Title, string SubTitle), ResultViewModel>(_results.Count);
        foreach (var existingItem in _results)
        {
            existingItems.TryAdd(ResultViewModelComparer.GetIdentityKey(existingItem), existingItem);
        }

        for (var i = 0; i < resultsList.Count; i++)
        {
            var newItem = resultsList[i];
            if (existingItems.TryGetValue(ResultViewModelComparer.GetIdentityKey(newItem), out var existing))
            {
                if (!ReferenceEquals(existing, newItem))
                {
                    existing.PluginResult = newItem.PluginResult;
                    existing.HistoryItem = newItem.HistoryItem;
                    existing.IconPath = newItem.IconPath;
                    existing.Glyph = newItem.Glyph;
                    existing.TitleHighlightData = newItem.TitleHighlightData;
                    existing.Score = newItem.Score;

                    resultsList[i] = existing;
                }
            }
        }

        // In-place collection synchronization to avoid recreating containers or resetting selection.
        // If the top item is identical, index 0 is never touched, so its ListBoxItem stays selected without animating.
        var count = Math.Min(_results.Count, resultsList.Count);
        for (var i = 0; i < count; i++)
        {
            if (!ReferenceEquals(_results[i], resultsList[i]))
            {
                _results[i] = resultsList[i];
            }
        }

        if (resultsList.Count > _results.Count)
        {
            for (var i = _results.Count; i < resultsList.Count; i++)
            {
                _results.Add(resultsList[i]);
            }
        }
        else if (resultsList.Count < _results.Count)
        {
            for (var i = _results.Count - 1; i >= resultsList.Count; i--)
            {
                _results.RemoveAt(i);
            }
        }

        // Select first item after replacement, unless the caller asked to keep the
        // previous selection (re-query) and that item survived the diff.
        var keptIndex = !reselect && previousSelection != null ? _results.IndexOf(previousSelection) : -1;
        if (keptIndex >= 0)
        {
            if (SelectedIndex != keptIndex)
                SelectedIndex = keptIndex;
            if (!ReferenceEquals(SelectedItem, previousSelection))
                SelectedItem = previousSelection;
        }
        else if (_results.Count > 0)
        {
            if (SelectedIndex != 0)
                SelectedIndex = 0;
            if (!ReferenceEquals(SelectedItem, _results[0]))
                SelectedItem = _results[0];
        }
        else
        {
            SelectedItem = null;
            SelectedIndex = -1;
        }
    }


    public void ReplaceResultsForPlugin(string pluginId, IEnumerable<ResultViewModel> pluginResults, bool reselect = true)
    {
        ReplaceResultsForPlugins([new KeyValuePair<string, IReadOnlyList<ResultViewModel>>(pluginId, pluginResults.ToList())], clearExisting: false, reselect: reselect);
    }

    public void ReplaceResultsForPlugins(IReadOnlyList<KeyValuePair<string, IReadOnlyList<ResultViewModel>>> pluginUpdates, bool clearExisting = false, bool reselect = true)
    {
        if (pluginUpdates.Count == 0 && !clearExisting)
            return;

        var updatedPluginIds = new HashSet<string>(pluginUpdates.Select(u => u.Key), StringComparer.Ordinal);
        var mergedResults = clearExisting
            ? new List<ResultViewModel>()
            : _results
                .Where(result => !updatedPluginIds.Contains(ResultViewModelComparer.GetPluginId(result)))
                .ToList();

        foreach (var update in pluginUpdates)
        {
            mergedResults.AddRange(update.Value);
        }

        ReplaceResults(mergedResults, reselect);
    }

    public void AddResult(ResultViewModel result)
    {
        result.Settings = _settings;
        _results.Add(result);

        // Select first item if nothing selected
        if (SelectedItem == null && _results.Count > 0)
        {
            SelectedIndex = 0;
            SelectedItem = _results[0];
        }
    }

    public void Clear()
    {
        _results.Clear();
        SelectedItem = null;
        SelectedIndex = -1;
    }

    public void SelectNextItem()
    {
        if (_results.Count == 0) return;

        var newIndex = SelectedIndex + 1;
        if (newIndex >= _results.Count)
        {
            newIndex = 0; // Wrap to beginning
        }

        SelectedIndex = newIndex;
        SelectedItem = _results[newIndex];
    }

    public void SelectPrevItem()
    {
        if (_results.Count == 0) return;

        var newIndex = SelectedIndex - 1;
        if (newIndex < 0)
        {
            newIndex = _results.Count - 1; // Wrap to end
        }

        SelectedIndex = newIndex;
        SelectedItem = _results[newIndex];
    }

    public void SelectNextPage() => SelectPage(_settings.MaxResultsToShow);

    public void SelectPrevPage() => SelectPage(-_settings.MaxResultsToShow);

    private void SelectPage(int offset)
    {
        var count = _results.Count;
        if (count == 0) return;

        var index = Math.Clamp(SelectedIndex + offset, 0, count - 1);
        SelectedIndex = index;
        SelectedItem = _results[index];
    }

    public void SelectFirstResult() => SelectIndex(0);

    public void SelectLastResult() => SelectIndex(_results.Count - 1);

    // Wraps around like the WPF ResultsViewModel.NewIndex.
    private void SelectIndex(int index)
    {
        var count = _results.Count;
        if (count == 0) return;

        index = ((index % count) + count) % count;
        SelectedIndex = index;
        SelectedItem = _results[index];
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < _results.Count)
        {
            SelectedItem = _results[value];
        }
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
    }

    /// <summary>
    /// Comparer for EditDiff - considers results equal if Title and SubTitle match.
    /// </summary>
    private class ResultViewModelComparer : IEqualityComparer<ResultViewModel>
    {
        public static readonly ResultViewModelComparer Instance = new();

        public bool Equals(ResultViewModel? x, ResultViewModel? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return GetIdentityKey(x).Equals(GetIdentityKey(y));
        }

        public int GetHashCode(ResultViewModel obj)
        {
            return GetIdentityKey(obj).GetHashCode();
        }

        // History rows are never replaced by a plugin's ResultsUpdated batch, even when recorded from that plugin.
        public static string GetPluginId(ResultViewModel item)
        {
            return item.HistoryItem != null ? string.Empty : item.PluginResult?.PluginID ?? string.Empty;
        }

        public static (string PluginId, string RecordKey, string Query, string Title, string SubTitle) GetIdentityKey(ResultViewModel item)
        {
            return (
                item.PluginResult?.PluginID ?? item.HistoryItem?.PluginID ?? string.Empty,
                item.PluginResult?.RecordKey ?? item.HistoryItem?.RecordKey ?? string.Empty,
                item.HistoryItem?.Query ?? string.Empty,
                item.Title,
                item.SubTitle);
        }
    }
}
