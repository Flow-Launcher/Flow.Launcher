using Avalonia.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class HotkeySettingsPage : UserControl
{
    public HotkeySettingsPage()
    {
        InitializeComponent();
        DataContext = new HotkeySettingsViewModel();
    }
}
