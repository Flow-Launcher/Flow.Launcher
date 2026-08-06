using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views.Avalonia;

public partial class CustomBrowserSettingWindow : Window
{
    public CustomBrowserSettingWindow(CustomBrowser browser)
    {
        InitializeComponent();
        DataContext = browser;
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
        Close();
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        Close();
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