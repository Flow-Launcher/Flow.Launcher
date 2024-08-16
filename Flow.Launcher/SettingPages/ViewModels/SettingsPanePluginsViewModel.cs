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
            PluginSettingsObject = _settings.PluginSettings.Plugins[plugin.Metadata.ID]
        })
        .ToList();

    public List<PluginViewModel> FilteredPluginViewModels => PluginViewModels
        .Where(v =>
            string.IsNullOrEmpty(FilterText) ||
            StringMatcher.FuzzySearch(FilterText, v.PluginPair.Metadata.Name).IsSearchPrecisionScoreMet() ||
            StringMatcher.FuzzySearch(FilterText, v.PluginPair.Metadata.Description).IsSearchPrecisionScoreMet()
        )
        .ToList();
}
