using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SettingPages.ViewModels;
using Page = ModernWpf.Controls.Page;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneTheme : Page
{
    private SettingsPaneThemeViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            var theme = Ioc.Default.GetRequiredService<Theme>();
            _viewModel = new SettingsPaneThemeViewModel(settings, theme);
            DataContext = _viewModel;
            InitializeComponent();
        }

        base.OnNavigatedTo(e);
    }
}
