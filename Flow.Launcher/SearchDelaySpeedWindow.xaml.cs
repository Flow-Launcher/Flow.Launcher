using System.Linq;
using System.Windows;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using static Flow.Launcher.SettingPages.ViewModels.SettingsPaneGeneralViewModel;

namespace Flow.Launcher;

public partial class SearchDelayTimeWindow : Window
{
    private readonly PluginViewModel _pluginViewModel;

    public SearchDelayTimeWindow(PluginViewModel pluginViewModel)
    {
        InitializeComponent();
        _pluginViewModel = pluginViewModel;
    }

    private void SearchDelayTimeWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        tbOldSearchDelayTime.Text = _pluginViewModel.SearchDelayTimeText;
        var searchDelayTimes = DropdownDataGeneric<SearchDelayTime>.GetValues<SearchDelayTimeData>("SearchDelayTime");
        SearchDelayTimeData selected = null;
        // Because default value is SearchDelayTime.Slow, we need to get selected value before adding default value
        if (_pluginViewModel.PluginSearchDelayTime != null)
        {
            selected = searchDelayTimes.FirstOrDefault(x => x.Value == _pluginViewModel.PluginSearchDelayTime);
        }
        // Add default value to the beginning of the list
        // When _pluginViewModel.PluginSearchDelay equals null, we will select this
        searchDelayTimes.Insert(0, new SearchDelayTimeData { Display = App.API.GetTranslation("default"), LocalizationKey = "default" });
        selected ??= searchDelayTimes.FirstOrDefault();
        tbDelay.ItemsSource = searchDelayTimes;
        tbDelay.SelectedItem = selected;
        tbDelay.Focus();
    }

    private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void btnDone_OnClick(object sender, RoutedEventArgs _)
    {
        // Update search delay time
        var selected = tbDelay.SelectedItem as SearchDelayTimeData;
        SearchDelayTime? changedValue = selected?.LocalizationKey != "default" ? selected.Value : null;
        _pluginViewModel.PluginSearchDelayTime = changedValue;

        // Update search delay time text and close window
        _pluginViewModel.OnSearchDelayTimeChanged();
        Close();
    }
}
