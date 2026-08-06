using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views.Avalonia;

public partial class SettingsControl : UserControl
{
    public SettingsControl(Settings settings)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settings, EditBrowser);
    }
    
    public SettingsControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    private async Task EditBrowser(CustomBrowser browser)
    {
        var window = new CustomBrowserSettingWindow(browser);
        if (VisualRoot is Window parentWindow)
        {
             await window.ShowDialog(parentWindow);
        }
    }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Func<CustomBrowser, Task> _editBrowserAction;

    public SettingsViewModel(Settings settings, Func<CustomBrowser, Task> editBrowserAction)
    {
        _settings = settings;
        _editBrowserAction = editBrowserAction;
    }
    
    public Settings Settings => _settings;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCustomBrowserCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCustomBrowserCommand))]
    private CustomBrowser _selectedCustomBrowser;

    public bool LoadChromeBookmark
    {
        get => _settings.LoadChromeBookmark;
        set
        {
            if (_settings.LoadChromeBookmark != value)
            {
                _settings.LoadChromeBookmark = value;
                OnPropertyChanged();
                _ = Task.Run(() => Main.ReloadAllBookmarks());
            }
        }
    }

    public bool LoadFirefoxBookmark
    {
        get => _settings.LoadFirefoxBookmark;
        set
        {
            if (_settings.LoadFirefoxBookmark != value)
            {
                _settings.LoadFirefoxBookmark = value;
                OnPropertyChanged();
                _ = Task.Run(() => Main.ReloadAllBookmarks());
            }
        }
    }

    public bool LoadEdgeBookmark
    {
        get => _settings.LoadEdgeBookmark;
        set
        {
            if (_settings.LoadEdgeBookmark != value)
            {
                _settings.LoadEdgeBookmark = value;
                OnPropertyChanged();
                _ = Task.Run(() => Main.ReloadAllBookmarks());
            }
        }
    }

    [RelayCommand]
    private async Task NewCustomBrowser()
    {
        var newBrowser = new CustomBrowser();
        await _editBrowserAction(newBrowser);
        
        if (!string.IsNullOrEmpty(newBrowser.Name) && !string.IsNullOrEmpty(newBrowser.DataDirectoryPath))
        {
            _settings.CustomChromiumBrowsers.Add(newBrowser);
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditCustomBrowser()
    {
        if (SelectedCustomBrowser is null) return;
        
        await _editBrowserAction(SelectedCustomBrowser);
        _ = Task.Run(() => Main.ReloadAllBookmarks());
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void DeleteCustomBrowser()
    {
        if (SelectedCustomBrowser is null) return;
        _settings.CustomChromiumBrowsers.Remove(SelectedCustomBrowser);
        _ = Task.Run(() => Main.ReloadAllBookmarks());
    }

    private bool CanEditOrDelete() => SelectedCustomBrowser != null;
}