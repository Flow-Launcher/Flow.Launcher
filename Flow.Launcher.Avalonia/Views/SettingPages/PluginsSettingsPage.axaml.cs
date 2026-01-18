using Avalonia.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class PluginsSettingsPage : UserControl
{
    public PluginsSettingsPage()
    {
        InitializeComponent();
        DataContext = new PluginsSettingsViewModel();
    }
}
