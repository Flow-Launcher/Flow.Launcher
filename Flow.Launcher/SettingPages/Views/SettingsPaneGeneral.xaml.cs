using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneGeneral
{
    private SettingsPaneGeneralViewModel _viewModel = null!;
    private SettingWindowViewModel _settingViewModel = null;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPaneGeneralViewModel>();
            _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page type
        _settingViewModel.PageType = typeof(SettingsPaneGeneral);
        base.OnNavigatedTo(e);
    }
}
