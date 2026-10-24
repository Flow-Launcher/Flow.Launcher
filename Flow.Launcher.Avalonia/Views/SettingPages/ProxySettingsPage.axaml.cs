using Avalonia.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class ProxySettingsPage : UserControl
{
    public ProxySettingsPage()
    {
        InitializeComponent();
        DataContext = new ProxySettingsViewModel();
    }
}
