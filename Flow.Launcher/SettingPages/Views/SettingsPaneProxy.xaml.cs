using System;
using System.Windows.Navigation;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneProxy
{
    private SettingsPaneProxyViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings, Updater: { } updater })
                throw new ArgumentException($"Settings are required for {nameof(SettingsPaneProxy)}.");
            _viewModel = new SettingsPaneProxyViewModel(settings, updater);
            DataContext = _viewModel;
            InitializeComponent();
        }

        base.OnNavigatedTo(e);
    }
}
