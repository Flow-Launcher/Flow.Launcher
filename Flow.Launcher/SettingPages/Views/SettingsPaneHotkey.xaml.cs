using System;
using System.Windows.Navigation;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneHotkey
{
    private SettingsPaneHotkeyViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings, MainViewModel: { } mainVM })
                throw new ArgumentException("Settings are required for SettingsPaneHotkey.");
            _viewModel = new SettingsPaneHotkeyViewModel(settings, mainVM);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
