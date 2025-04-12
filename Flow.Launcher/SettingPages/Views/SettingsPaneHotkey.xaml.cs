using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneHotkey
{
    private SettingsPaneHotkeyViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            _viewModel = new SettingsPaneHotkeyViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
