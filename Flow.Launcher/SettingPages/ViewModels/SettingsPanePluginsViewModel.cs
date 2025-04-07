using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using ModernWpf.Controls;

#nullable enable

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPanePluginsViewModel : BaseModel
{
    private readonly Settings _settings;

    public class DisplayModeData : DropdownDataGeneric<DisplayMode> { }

    public List<DisplayModeData> DisplayModes { get; } =
        DropdownDataGeneric<DisplayMode>.GetValues<DisplayModeData>("DisplayMode");

    private DisplayMode _selectedDisplayMode = DisplayMode.OnOff;
    public DisplayMode SelectedDisplayMode
    {
        get => _selectedDisplayMode;
        set
        {
            if (_selectedDisplayMode != value)
            {
                _selectedDisplayMode = value;
                OnPropertyChanged();
                UpdateDisplayModeFromSelection();
            }
        }
    }

    private bool _isOnOffSelected = true;
    public bool IsOnOffSelected
    {
        get => _isOnOffSelected;
        set
        {
            if (_isOnOffSelected != value)
            {
                _isOnOffSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isPrioritySelected;
    public bool IsPrioritySelected
    {
        get => _isPrioritySelected;
        set
        {
            if (_isPrioritySelected != value)
            {
                _isPrioritySelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSearchDelaySelected;
    public bool IsSearchDelaySelected
    {
        get => _isSearchDelaySelected;
        set
        {
            if (_isSearchDelaySelected != value)
            {
                _isSearchDelaySelected = value;
                OnPropertyChanged();
            }
        }
    }

    public SettingsPanePluginsViewModel(Settings settings)
    {
        _settings = settings;
        UpdateEnumDropdownLocalizations();
    }

    public string FilterText { get; set; } = string.Empty;

    public PluginViewModel? SelectedPlugin { get; set; }

    private IEnumerable<PluginViewModel>? _pluginViewModels;
    private IEnumerable<PluginViewModel> PluginViewModels => _pluginViewModels ??= PluginManager.AllPlugins
        .OrderBy(plugin => plugin.Metadata.Disabled)
        .ThenBy(plugin => plugin.Metadata.Name)
        .Select(plugin => new PluginViewModel
        {
            PluginPair = plugin,
            PluginSettingsObject = _settings.PluginSettings.GetPluginSettings(plugin.Metadata.ID)
        })
        .Where(plugin => plugin.PluginSettingsObject != null)
        .ToList();

    public List<PluginViewModel> FilteredPluginViewModels => PluginViewModels
        .Where(v =>
            string.IsNullOrEmpty(FilterText) ||
            StringMatcher.FuzzySearch(FilterText, v.PluginPair.Metadata.Name).IsSearchPrecisionScoreMet() ||
            StringMatcher.FuzzySearch(FilterText, v.PluginPair.Metadata.Description).IsSearchPrecisionScoreMet()
        )
        .ToList();

    [RelayCommand]
    private async Task OpenHelperAsync()
    {
        var helpDialog = new ContentDialog()
        {
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["changePriorityWindow"],
                        FontSize = 18,
                        Margin = new Thickness(0, 0, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["priority_tips"],
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["searchDelayTimeTitle"],
                        FontSize = 18,
                        Margin = new Thickness(0, 24, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["searchDelayTimeTips"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },

            PrimaryButtonText = (string)Application.Current.Resources["commonOK"],
            CornerRadius = new CornerRadius(8),
            Style = (Style)Application.Current.Resources["ContentDialog"]
        };

        await helpDialog.ShowAsync();
    }

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<DisplayMode>.UpdateLabels(DisplayModes);
    }

    private void UpdateDisplayModeFromSelection()
    {
        switch (SelectedDisplayMode)
        {
            case DisplayMode.Priority:
                IsOnOffSelected = false;
                IsPrioritySelected = true;
                IsSearchDelaySelected = false;
                break;
            case DisplayMode.SearchDelay:
                IsOnOffSelected = false;
                IsPrioritySelected = false;
                IsSearchDelaySelected = true;
                break;
            default:
                IsOnOffSelected = true;
                IsPrioritySelected = false;
                IsSearchDelaySelected = false;
                break;
        }
    }
}

public enum DisplayMode
{
    OnOff,
    Priority,
    SearchDelay
}
