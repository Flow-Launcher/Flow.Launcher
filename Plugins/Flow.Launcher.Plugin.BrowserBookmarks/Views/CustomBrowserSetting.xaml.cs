using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System.Windows;
using System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Views;

/// <summary>
/// Interaction logic for CustomBrowserSetting.xaml
/// </summary>
public partial class CustomBrowserSettingWindow : Window
{
    private readonly CustomBrowser _currentCustomBrowser;
    
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

    private void ConfirmEditCustomBrowser(object sender, RoutedEventArgs e)
    {
        var editBrowser = (CustomBrowser)DataContext;
        _currentCustomBrowser.Name = editBrowser.Name;
        _currentCustomBrowser.DataDirectoryPath = editBrowser.DataDirectoryPath;
        _currentCustomBrowser.BrowserType = editBrowser.BrowserType;
        DialogResult = true;
        Close();
    }

    private void CancelEditCustomBrowser(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ConfirmEditCustomBrowser(sender, e);
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelEditCustomBrowser(sender, e);
        }
    }

    private void OnSelectPathClick(object sender, RoutedEventArgs e)
    {
        var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var editBrowser = (CustomBrowser)DataContext;
            editBrowser.DataDirectoryPath = dialog.SelectedPath;
        }
    }
}
