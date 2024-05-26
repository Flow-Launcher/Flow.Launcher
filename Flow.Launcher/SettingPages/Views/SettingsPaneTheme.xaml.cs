using System;
using System.Windows.Controls;
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

    private void Reset_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        //QueryBoxFont = "Segoe UI";
        //QueryBoxFontStyle = "Normal";
        //QueryBoxFontWeight = "Normal";
        //QueryBoxFontStretch = "Normal";
        //QueryBoxFontSize = 20;
        QueryBoxFontSize.Value = 20;

        //ResultFont = "Segoe UI";
        //ResultFontStyle = "Normal";
        //ResultFontWeight = "Normal";
        //ResultFontStretch = "Normal";
        //ResultItemFontSize = 16;
        resultItemFontSize.Value = 16;

        //ResultSubFont = "Segoe UI";
        //ResultSubFontStyle = "Normal";
        //ResultSubFontWeight = "Normal";
        //ResultSubFontStretch = "Normal";
        //ResultSubItemFontSize = 13;
        resultSubItemFontSize.Value = 13;
        //ItemHeightSize = 58;
        //WindowHeightSize = 42;
        WindowHeightValue.Value = 42;
        ItemHeightValue.Value = 58;
        //_viewModel.ResetCustomize();
    }
}
