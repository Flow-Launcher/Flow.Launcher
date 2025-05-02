using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneTheme
{
    private SettingsPaneThemeViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPaneThemeViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
