using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Plugin.BrowserBookmarks.Models;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Views;

[INotifyPropertyChanged]
public partial class SettingsControl : UserControl
{
    public Settings Settings { get; }
    public CustomBrowser SelectedCustomBrowser { get; set; }

    public SettingsControl(Settings settings)
    {
        Settings = settings;
        InitializeComponent();
    }

    // Note: The auto-reload logic will be handled by the Main class listening to settings changes
    // This code-behind is now simpler.

    private void NewCustomBrowser(object sender, RoutedEventArgs e)
    {
        var newBrowser = new CustomBrowser();
        var window = new CustomBrowserSettingWindow(newBrowser);
        if (window.ShowDialog() == true)
        {
            Settings.CustomBrowsers.Add(newBrowser);
        }
    }

    private void DeleteCustomBrowser(object sender, RoutedEventArgs e)
    {
        if (CustomBrowsers.SelectedItem is CustomBrowser selectedCustomBrowser)
        {
            Settings.CustomBrowsers.Remove(selectedCustomBrowser);
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
        window.ShowDialog();
    }
}