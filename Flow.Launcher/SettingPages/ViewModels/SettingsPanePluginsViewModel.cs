using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

#nullable enable

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPanePluginsViewModel : BaseModel
{
    private readonly Settings _settings;

    public SettingsPanePluginsViewModel(Settings settings)
    {
        _settings = settings;
    }

    public string FilterText { get; set; } = string.Empty;

    public PluginViewModel? SelectedPlugin { get; set; }
    private IEnumerable<PluginViewModel>? _pluginViewModels;
    private IEnumerable<PluginViewModel> PluginViewModels => _pluginViewModels ??= PluginManager.AllPlugins
        .OrderBy(x => x.Metadata.Disabled)
        .ThenBy(y => y.Metadata.Name)
        .Select(p => new PluginViewModel { PluginPair = p })
        .ToList();
    public List<PluginViewModel> FilteredPluginViewModels => PluginViewModels
        .Where(v =>
            string.IsNullOrEmpty(FilterText) ||
            StringMatcher.FuzzySearch(FilterText, v.PluginPair.Metadata.Name).IsSearchPrecisionScoreMet() ||
            StringMatcher.FuzzySearch(FilterText, v.PluginPair.Metadata.Description).IsSearchPrecisionScoreMet()
        )
        .ToList();

    [RelayCommand]
    private void TogglePlugin()
    {
        if (SelectedPlugin is null) return;
        var id = SelectedPlugin.PluginPair.Metadata.ID;
        // used to sync the current status from the plugin manager into the setting to keep consistency after save
        _settings.PluginSettings.Plugins[id].Disabled = SelectedPlugin.PluginPair.Metadata.Disabled;
    }
}
