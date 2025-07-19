using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneAbout
{
    private SettingsPaneAboutViewModel _viewModel = null!;
    private readonly SettingWindowViewModel _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page type
        _settingViewModel.PageType = typeof(SettingsPaneAbout);

        // If the navigation is not triggered by button click, view model will be null again
        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPaneAboutViewModel>();
            DataContext = _viewModel;
        }
        if (!IsInitialized)
        {
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
