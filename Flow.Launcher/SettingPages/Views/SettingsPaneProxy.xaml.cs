using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneProxy
{
    private SettingsPaneProxyViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            var updater = Ioc.Default.GetRequiredService<Updater>();
            _viewModel = new SettingsPaneProxyViewModel(settings, updater);
            DataContext = _viewModel;
            InitializeComponent();
        }

        base.OnNavigatedTo(e);
    }
}
