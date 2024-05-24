using System;
using System.Windows.Navigation;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneGeneral
{
    private SettingsPaneGeneralViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings, Updater: {} updater, Portable: {} portable })
                throw new ArgumentException("Settings, Updater and Portable are required for SettingsPaneGeneral.");
            _viewModel = new SettingsPaneGeneralViewModel(settings, updater, portable);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
