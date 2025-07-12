using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views;

[INotifyPropertyChanged]
public partial class SettingsControl
{
    public Settings Settings { get; }
    public CustomBrowser SelectedCustomBrowser { get; set; }

    public SettingsControl(Settings settings)
    {
        Settings = settings;
        InitializeComponent();
    }

    public bool LoadChromeBookmark
    {
        get => Settings.LoadChromeBookmark;
        set
        {
            Settings.LoadChromeBookmark = value;
            _ = Main.ReloadAllBookmarksAsync();
        }
    }

    public bool LoadFirefoxBookmark
    {
        get => Settings.LoadFirefoxBookmark;
        set
        {
            Settings.LoadFirefoxBookmark = value;
            _ = Main.ReloadAllBookmarksAsync();
        }
    }

    public bool LoadEdgeBookmark
    {
        get => Settings.LoadEdgeBookmark;
        set
        {
            Settings.LoadEdgeBookmark = value;
            _ = Main.ReloadAllBookmarksAsync();
        }
    }

    public bool EnableFavicons
    {
        get => Settings.EnableFavicons;
        set
        {
            Settings.EnableFavicons = value;
            _ = Main.ReloadAllBookmarksAsync();
            OnPropertyChanged();
        }
    }

    public bool OpenInNewBrowserWindow
    {
        get => Settings.OpenInNewBrowserWindow;
        set
        {
            Settings.OpenInNewBrowserWindow = value;
            OnPropertyChanged();
        }
    }

    private void NewCustomBrowser(object sender, RoutedEventArgs e)
    {
        var newBrowser = new CustomBrowser();
        var window = new CustomBrowserSettingWindow(newBrowser);
        var result = window.ShowDialog() ?? false;
        if (result)
        {
            Settings.CustomChromiumBrowsers.Add(newBrowser);
            _ = Main.ReloadAllBookmarksAsync();
        }
    }

    private void DeleteCustomBrowser(object sender, RoutedEventArgs e)
    {
        if (CustomBrowsers.SelectedItem is CustomBrowser selectedCustomBrowser)
        {
            Settings.CustomChromiumBrowsers.Remove(selectedCustomBrowser);
            _ = Main.ReloadAllBookmarksAsync();
        }
    }

    private void MouseDoubleClickOnSelectedCustomBrowser(object sender, MouseButtonEventArgs e)
    {
        EditSelectedCustomBrowser();
    }

    private void Others_Click(object sender, RoutedEventArgs e)
    {
        CustomBrowsersList.Visibility = CustomBrowsersList.Visibility switch
        {
            Visibility.Collapsed => Visibility.Visible,
            _ => Visibility.Collapsed
        };
    }

    private void EditCustomBrowser(object sender, RoutedEventArgs e)
    {
        EditSelectedCustomBrowser();
    }

    private void EditSelectedCustomBrowser()
    {
        if (SelectedCustomBrowser is null)
            return;

        var window = new CustomBrowserSettingWindow(SelectedCustomBrowser);
        var result = window.ShowDialog() ?? false;
        if (result)
        {
            _ = Main.ReloadAllBookmarksAsync();
        }
    }
}