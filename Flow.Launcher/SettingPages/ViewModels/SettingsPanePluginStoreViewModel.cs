using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPanePluginStoreViewModel : BaseModel
{
    public string FilterText { get; set; } = string.Empty;

    public IList<PluginStoreItemViewModel> ExternalPlugins => PluginsManifest.UserPlugins
        .Select(p => new PluginStoreItemViewModel(p))
        .OrderByDescending(p => p.Category == PluginStoreItemViewModel.NewRelease)
        .ThenByDescending(p => p.Category == PluginStoreItemViewModel.RecentlyUpdated)
        .ThenByDescending(p => p.Category == PluginStoreItemViewModel.None)
        .ThenByDescending(p => p.Category == PluginStoreItemViewModel.Installed)
        .ToList();

    [RelayCommand]
    private async Task RefreshExternalPluginsAsync()
    {
        await PluginsManifest.UpdateManifestAsync();
        OnPropertyChanged(nameof(ExternalPlugins));
    }

    public bool SatisfiesFilter(PluginStoreItemViewModel plugin)
    {
        return string.IsNullOrEmpty(FilterText) ||
               StringMatcher.FuzzySearch(FilterText, plugin.Name).IsSearchPrecisionScoreMet() ||
               StringMatcher.FuzzySearch(FilterText, plugin.Description).IsSearchPrecisionScoreMet();
    }
}
