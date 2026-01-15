using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// Simplified MainViewModel for the Avalonia version.
/// This will eventually be unified with the WPF MainViewModel.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly Settings _settings;

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private string _querySuggestionText = string.Empty;

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
        
        // Add some demo results for testing
        AddDemoResults();
    }

    partial void OnQueryTextChanged(string value)
    {
        // Simulate query execution
        if (!string.IsNullOrWhiteSpace(value))
        {
            IsQueryRunning = true;
            HasResults = true;
            
            // Simulate search
            Task.Delay(100).ContinueWith(_ =>
            {
                IsQueryRunning = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        else
        {
            HasResults = false;
            IsQueryRunning = false;
        }
    }

    private void AddDemoResults()
    {
        // Add demo results for UI testing
        Results.AddResult(new ResultViewModel
        {
            Title = "Welcome to Flow Launcher (Avalonia)",
            SubTitle = "This is a demo result - Avalonia migration in progress",
            IconPath = "Images/app.png"
        });
        
        Results.AddResult(new ResultViewModel
        {
            Title = "Settings",
            SubTitle = "Open Flow Launcher settings",
            IconPath = "Images/app.png"
        });
        
        Results.AddResult(new ResultViewModel
        {
            Title = "Notepad",
            SubTitle = "C:\\Windows\\System32\\notepad.exe",
            IconPath = "Images/app.png"
        });

        Results.AddResult(new ResultViewModel
        {
            Title = "Calculator",
            SubTitle = "Microsoft Calculator",
            IconPath = "Images/app.png"
        });

        HasResults = true;
    }

    [RelayCommand]
    private void Esc()
    {
        QueryText = string.Empty;
    }

    [RelayCommand]
    private void OpenResult(object? parameter)
    {
        var selectedResult = Results.SelectedItem;
        if (selectedResult != null)
        {
            // Execute the result action
            System.Diagnostics.Debug.WriteLine($"Opening result: {selectedResult.Title}");
        }
    }

    [RelayCommand]
    private void SelectNextItem()
    {
        Results.SelectNextItem();
    }

    [RelayCommand]
    private void SelectPrevItem()
    {
        Results.SelectPrevItem();
    }

    [RelayCommand]
    private void AutocompleteQuery()
    {
        if (Results.SelectedItem != null)
        {
            QueryText = Results.SelectedItem.Title;
        }
    }

    [RelayCommand]
    private void ReloadPluginData()
    {
        // Placeholder for plugin data reload
        System.Diagnostics.Debug.WriteLine("Reloading plugin data...");
    }

    [RelayCommand]
    private void ReQuery()
    {
        // Placeholder for re-query
        System.Diagnostics.Debug.WriteLine("Re-querying...");
    }
}
