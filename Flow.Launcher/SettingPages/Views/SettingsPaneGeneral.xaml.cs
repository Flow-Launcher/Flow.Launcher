using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
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
            var settings = Ioc.Default.GetRequiredService<Settings>();
            var updater = Ioc.Default.GetRequiredService<Updater>();
            var portable = Ioc.Default.GetRequiredService<Portable>();
            _viewModel = new SettingsPaneGeneralViewModel(settings, updater, portable);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
