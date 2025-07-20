using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views;

/// <summary>
/// Interaction logic for CustomBrowserSetting.xaml
/// </summary>
public partial class CustomBrowserSettingWindow : Window
{
    private CustomBrowser _currentCustomBrowser;
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
        CustomBrowser editBrowser = (CustomBrowser)DataContext;
        _currentCustomBrowser.Name = editBrowser.Name;
        _currentCustomBrowser.DataDirectoryPath = editBrowser.DataDirectoryPath;
        _currentCustomBrowser.BrowserType = editBrowser.BrowserType;
        DialogResult = true;
        Close();
    }

    private void CancelEditCustomBrowser(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmEditCustomBrowser(sender, e);
        }
    }

    private void OnSelectPathClick(object sender, RoutedEventArgs e)
    {
        var dialog = new FolderBrowserDialog();
        dialog.ShowDialog();
        CustomBrowser editBrowser = (CustomBrowser)DataContext;
        editBrowser.DataDirectoryPath = dialog.SelectedPath;
    }
}
