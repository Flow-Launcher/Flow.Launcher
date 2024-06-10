using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
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
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings })
                throw new ArgumentException($"Settings are required for {nameof(SettingsPaneTheme)}.");
            _viewModel = new SettingsPaneThemeViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }

        base.OnNavigatedTo(e);
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.UpdateColorScheme();
    }
}
