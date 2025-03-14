using System;
using System.Windows.Navigation;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneAbout
{
    private SettingsPaneAboutViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings, Updater: { } updater })
                throw new ArgumentException("Settings are required for SettingsPaneAbout.");
            _viewModel = new SettingsPaneAboutViewModel(settings, updater);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        App.API.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
