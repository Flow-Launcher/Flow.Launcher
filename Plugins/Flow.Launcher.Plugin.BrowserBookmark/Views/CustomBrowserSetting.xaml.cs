using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.BrowserBookmark.ViewModels;
using System.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views;

/// <summary>
/// Interaction logic for CustomBrowserSetting.xaml
/// </summary>
public partial class CustomBrowserSetting : Window
{
    private readonly CustomBrowserSettingViewModel _viewModel;

    public CustomBrowserSetting(CustomBrowser browser)
    {
        InitializeComponent();
        _viewModel = new CustomBrowserSettingViewModel(browser, result =>
        {
            DialogResult = result;
            Close();
        });
        DataContext = _viewModel;
    }
    
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            _viewModel.SaveCommand.Execute(null);
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            _viewModel.CancelCommand.Execute(null);
        }
    }
}
