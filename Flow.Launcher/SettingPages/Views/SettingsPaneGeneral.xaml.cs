using System;
using System.Windows.Navigation;
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
            if (e.ExtraData is not Settings settings)
                throw new ArgumentException("Settings is not passed to General page");
            _viewModel = new SettingsPaneGeneralViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
}
