using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneHotkey
{
    private SettingsPaneHotkeyViewModel _viewModel = null!;
    private readonly SettingWindowViewModel _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page type
        _settingViewModel.PageType = typeof(SettingsPaneHotkey);

        // If the navigation is not triggered by button click, view model will be null again
        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPaneHotkeyViewModel>();
            DataContext = _viewModel;
        }
        if (!IsInitialized)
        {
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
