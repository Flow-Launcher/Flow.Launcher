using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views.Avalonia;

internal partial class CustomBrowserSettingWindow : Window
{
    private readonly CustomBrowser? _currentCustomBrowser;

    public CustomBrowserSettingWindow(CustomBrowser browser)
    {
        InitializeComponent();
        _currentCustomBrowser = browser;
        DataContext = new CustomBrowser
        {
            Name = browser.Name,
            DataDirectoryPath = browser.DataDirectoryPath,
            BrowserType = browser.BrowserType,
        };
    }
    
    public CustomBrowserSettingWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        if (_currentCustomBrowser is not null && DataContext is CustomBrowser editBrowser)
        {
            _currentCustomBrowser.Name = editBrowser.Name;
            _currentCustomBrowser.DataDirectoryPath = editBrowser.DataDirectoryPath;
            _currentCustomBrowser.BrowserType = editBrowser.BrowserType;
        }

        Close(true);
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Bookmark Data Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            if (DataContext is CustomBrowser browser)
            {
                browser.DataDirectoryPath = folders[0].Path.LocalPath;
            }
        }
    }
}