using System.Windows.Navigation;
using Flow.Launcher.Core;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.Infrastructure.UserSettings;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneAbout
{
    private SettingsPaneAboutViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            var updater = Ioc.Default.GetRequiredService<Updater>();
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
