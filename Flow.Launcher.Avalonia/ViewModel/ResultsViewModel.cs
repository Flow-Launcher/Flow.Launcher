using System;
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
    private readonly SourceList<ResultViewModel> _sourceList = new();
    private readonly ReadOnlyObservableCollection<ResultViewModel> _results;
    private readonly IDisposable _subscription;

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
    public ReadOnlyObservableCollection<ResultViewModel> Results => _results;

    public Settings Settings => _settings;

    public int MaxHeight => (int)(_settings.MaxResultsToShow * _settings.ItemHeightSize);

    public ResultsViewModel(Settings settings)
    {
        _settings = settings;

        // Connect SourceList to sorted ReadOnlyObservableCollection
        _subscription = _sourceList.Connect()
            .Sort(SortExpressionComparer<ResultViewModel>.Descending(r => r.Score))
            .Bind(out _results)
            .Subscribe();
    }

    /// <summary>
    /// Replace all results with new ones using atomic Edit to prevent flickering.
    /// Edit batches changes and fires only one notification at the end.
    /// </summary>
    public void ReplaceResults(IEnumerable<ResultViewModel> newResults)
    {
        var resultsList = newResults.ToList();
        foreach (var r in resultsList)
        {
            r.Settings = _settings;
        }

        // EditDiff calculates minimal changes needed - items with same Title+SubTitle are kept
        _sourceList.EditDiff(resultsList, ResultViewModelComparer.Instance);

        // Select first item after replacement
        if (_results.Count > 0)
        {
            SelectedIndex = 0;
            SelectedItem = _results[0];
        }
        else
        {
            SelectedItem = null;
            SelectedIndex = -1;
        }
    }

    public void AddResult(ResultViewModel result)
    {
        result.Settings = _settings;
        _sourceList.Add(result);

        // Select first item if nothing selected
        if (SelectedItem == null && _results.Count > 0)
        {
            SelectedIndex = 0;
            SelectedItem = _results[0];
        }
    }

    public void Clear()
    {
        _sourceList.Clear();
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

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < _results.Count)
        {
            SelectedItem = _results[value];
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _sourceList.Dispose();
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
            return x.Title == y.Title && x.SubTitle == y.SubTitle;
        }

        public int GetHashCode(ResultViewModel obj)
        {
            return HashCode.Combine(obj.Title, obj.SubTitle);
        }
    }
}
