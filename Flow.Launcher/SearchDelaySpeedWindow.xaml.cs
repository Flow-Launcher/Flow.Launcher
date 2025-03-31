using System.Linq;
using System.Windows;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using static Flow.Launcher.SettingPages.ViewModels.SettingsPaneGeneralViewModel;

namespace Flow.Launcher;

public partial class SearchDelaySpeedWindow : Window
{
    private readonly PluginViewModel _pluginViewModel;

    public SearchDelaySpeedWindow(PluginViewModel pluginViewModel)
    {
        InitializeComponent();
        _pluginViewModel = pluginViewModel;
    }

    private void SearchDelaySpeed_OnLoaded(object sender, RoutedEventArgs e)
    {
        tbOldSearchDelaySpeed.Text = _pluginViewModel.SearchDelaySpeedText;
        var searchDelaySpeeds = DropdownDataGeneric<SearchDelaySpeeds>.GetValues<SearchDelaySpeedData>("SearchDelaySpeed");
        SearchDelaySpeedData selected = null;
        // Because default value is SearchDelaySpeeds.Slow, we need to get selected value before adding default value
        if (_pluginViewModel.PluginSearchDelay != null)
        {
            selected = searchDelaySpeeds.FirstOrDefault(x => x.Value == _pluginViewModel.PluginSearchDelay);
        }
        // Add default value to the beginning of the list
        // When _pluginViewModel.PluginSearchDelay equals null, we will select this
        searchDelaySpeeds.Insert(0, new SearchDelaySpeedData { Display = App.API.GetTranslation(PluginViewModel.DefaultLocalizationKey), LocalizationKey = PluginViewModel.DefaultLocalizationKey });
        selected ??= searchDelaySpeeds.FirstOrDefault();
        tbDelay.ItemsSource = searchDelaySpeeds;
        tbDelay.SelectedItem = selected;
        tbDelay.Focus();
    }

    private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void btnDone_OnClick(object sender, RoutedEventArgs _)
    {
        // Update search delay speed
        var selected = tbDelay.SelectedItem as SearchDelaySpeedData;
        SearchDelaySpeeds? changedValue = selected?.LocalizationKey != PluginViewModel.DefaultLocalizationKey ? selected.Value : null;
        _pluginViewModel.PluginSearchDelay = changedValue;

        // Update search delay speed text and close window
        _pluginViewModel.OnSearchDelaySpeedChanged();
        Close();
    }
}
