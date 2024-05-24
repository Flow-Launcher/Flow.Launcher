using System.Windows;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Windows.Input;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views;

public partial class SettingsControl : INotifyPropertyChanged
{
    public Settings Settings { get; }

    public CustomBrowser SelectedCustomBrowser { get; set; }

    public bool LoadChromeBookmark
    {
        get => Settings.LoadChromeBookmark;
        set
        {
            Settings.LoadChromeBookmark = value;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    public bool LoadFirefoxBookmark
    {
        get => Settings.LoadFirefoxBookmark;
        set
        {
            Settings.LoadFirefoxBookmark = value;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    public bool LoadEdgeBookmark
    {
        get => Settings.LoadEdgeBookmark;
        set
        {
            Settings.LoadEdgeBookmark = value;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    public bool OpenInNewBrowserWindow
    {
        get => Settings.OpenInNewBrowserWindow;
        set
        {
            Settings.OpenInNewBrowserWindow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenInNewBrowserWindow)));
        }
    }

    public SettingsControl(Settings settings)
    {
        Settings = settings;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void NewCustomBrowser(object sender, RoutedEventArgs e)
    {
        var newBrowser = new CustomBrowser();
        var window = new CustomBrowserSettingWindow(newBrowser);
        window.ShowDialog();
        if (newBrowser is not
            {
                Name: null,
                DataDirectoryPath: null
            })
        {
            Settings.CustomChromiumBrowsers.Add(newBrowser);
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    private void DeleteCustomBrowser(object sender, RoutedEventArgs e)
    {
        if (CustomBrowsers.SelectedItem is CustomBrowser selectedCustomBrowser)
        {
            Settings.CustomChromiumBrowsers.Remove(selectedCustomBrowser);
            _ = Task.Run(() => Main.ReloadAllBookmarks());
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
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }
}
