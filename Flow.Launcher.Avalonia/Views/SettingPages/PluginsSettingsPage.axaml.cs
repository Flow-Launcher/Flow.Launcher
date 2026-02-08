using Avalonia.Controls;
using Avalonia.Interactivity;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class PluginsSettingsPage : UserControl
{
    public PluginsSettingsPage()
    {
        InitializeComponent();
        DataContext = new PluginsSettingsViewModel();
    }

    private void ClearSearchText_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PluginsSettingsViewModel vm)
        {
            vm.SearchText = string.Empty;
        }
    }
}
