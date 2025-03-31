using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

#nullable enable

namespace Flow.Launcher.SettingPages.ViewModels;

public class SettingsPanePluginsViewModel : BaseModel
{
    private readonly Settings _settings;

    private bool _isOnOffSelected = true;
    public bool IsOnOffSelected
    {
        get => _isOnOffSelected;
        set
        {
            if (_isOnOffSelected != value)
            {
                _isOnOffSelected = value;
                OnPropertyChanged(nameof(IsOnOffSelected));
                UpdateDisplayMode();
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
                OnPropertyChanged(nameof(IsPrioritySelected));
                UpdateDisplayMode();
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
                OnPropertyChanged(nameof(IsSearchDelaySelected));
                UpdateDisplayMode();
            }
        }
    }

    private string _currentDisplayMode = "OnOff";
    public string CurrentDisplayMode
    {
        get => _currentDisplayMode;
        set
        {
            if (_currentDisplayMode != value)
            {
                _currentDisplayMode = value;
                OnPropertyChanged(nameof(CurrentDisplayMode));
            }
        }
    }

    private void UpdateDisplayMode()
    {
        if (IsOnOffSelected)
            CurrentDisplayMode = "OnOff";
        else if (IsPrioritySelected)
            CurrentDisplayMode = "Priority";
        else if (IsSearchDelaySelected)
            CurrentDisplayMode = "SearchDelay";
    }

    public SettingsPanePluginsViewModel(Settings settings)
    {
        _settings = settings;
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
}
