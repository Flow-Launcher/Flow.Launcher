using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.BrowserBookmark.ViewModels;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views;

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
        if (e.Key == Key.Enter)
        {
            if (_viewModel.SaveCommand.CanExecute(null))
            {
                _viewModel.SaveCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CancelCommand.Execute(null);
        }
    }
}
