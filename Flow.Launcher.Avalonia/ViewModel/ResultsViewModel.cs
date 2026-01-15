using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// ViewModel for the results list.
/// </summary>
public partial class ResultsViewModel : ObservableObject
{
    private readonly Settings _settings;

    [ObservableProperty]
    private ObservableCollection<ResultViewModel> _results = new();

    [ObservableProperty]
    private ResultViewModel? _selectedItem;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isVisible = true;

    public Settings Settings => _settings;

    public int MaxHeight => (int)(_settings.MaxResultsToShow * _settings.ItemHeightSize);

    public ResultsViewModel(Settings settings)
    {
        _settings = settings;
    }

    public void AddResult(ResultViewModel result)
    {
        result.Settings = _settings;
        Results.Add(result);
        
        // Select first item if nothing selected
        if (SelectedItem == null && Results.Count > 0)
        {
            SelectedIndex = 0;
            SelectedItem = Results[0];
        }
    }

    public void Clear()
    {
        Results.Clear();
        SelectedItem = null;
        SelectedIndex = -1;
    }

    public void SelectNextItem()
    {
        if (Results.Count == 0) return;
        
        var newIndex = SelectedIndex + 1;
        if (newIndex >= Results.Count)
        {
            newIndex = 0; // Wrap to beginning
        }
        
        SelectedIndex = newIndex;
        SelectedItem = Results[newIndex];
    }

    public void SelectPrevItem()
    {
        if (Results.Count == 0) return;
        
        var newIndex = SelectedIndex - 1;
        if (newIndex < 0)
        {
            newIndex = Results.Count - 1; // Wrap to end
        }
        
        SelectedIndex = newIndex;
        SelectedItem = Results[newIndex];
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < Results.Count)
        {
            SelectedItem = Results[value];
        }
    }
}
