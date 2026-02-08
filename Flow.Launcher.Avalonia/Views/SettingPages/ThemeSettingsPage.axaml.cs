using Avalonia.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class ThemeSettingsPage : UserControl
{
    public ThemeSettingsPage()
    {
        InitializeComponent();
        DataContext = new ThemeSettingsViewModel();
    }
}
