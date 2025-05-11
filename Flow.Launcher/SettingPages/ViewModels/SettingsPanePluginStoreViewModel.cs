using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPanePluginStoreViewModel : BaseModel
{
    private string filterText = string.Empty;
    public string FilterText
    {
        get => filterText;
        set
        {
            if (filterText != value)
            {
                filterText = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showDotNet = true;
    public bool ShowDotNet
    {
        get => showDotNet;
        set
        {
            if (showDotNet != value)
            {
                showDotNet = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showPython = true;
    public bool ShowPython
    {
        get => showPython;
        set
        {
            if (showPython != value)
            {
                showPython = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showNodeJs = true;
    public bool ShowNodeJs
    {
        get => showNodeJs;
        set
        {
            if (showNodeJs != value)
            {
                showNodeJs = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showExecutable = true;
    public bool ShowExecutable
    {
        get => showExecutable;
        set
        {
            if (showExecutable != value)
            {
                showExecutable = value;
                OnPropertyChanged();
            }
        }
    }

    public IList<PluginStoreItemViewModel> ExternalPlugins => App.API.GetPluginManifest()?
        .Select(p => new PluginStoreItemViewModel(p))
        .OrderByDescending(p => p.Category == PluginStoreItemViewModel.NewRelease)
        .ThenByDescending(p => p.Category == PluginStoreItemViewModel.RecentlyUpdated)
        .ThenByDescending(p => p.Category == PluginStoreItemViewModel.None)
        .ThenByDescending(p => p.Category == PluginStoreItemViewModel.Installed)
        .ToList();

    [RelayCommand]
    private async Task RefreshExternalPluginsAsync()
    {
        if (await App.API.UpdatePluginManifestAsync())
        {
            OnPropertyChanged(nameof(ExternalPlugins));
        }
    }

    public bool SatisfiesFilter(PluginStoreItemViewModel plugin)
    {
        // Check plugin language
        var pluginShown = false;
        if (AllowedLanguage.IsDotNet(plugin.Language))
        {
            pluginShown = ShowDotNet;
        }
        else if (AllowedLanguage.IsPython(plugin.Language))
        {
            pluginShown = ShowPython;
        }
        else if (AllowedLanguage.IsNodeJs(plugin.Language))
        {
            pluginShown = ShowNodeJs;
        }
        else if (AllowedLanguage.IsExecutable(plugin.Language))
        {
            pluginShown = ShowExecutable;
        }
        if (!pluginShown) return false;

        // Check plugin name & description
        return string.IsNullOrEmpty(FilterText) ||
            App.API.FuzzySearch(FilterText, plugin.Name).IsSearchPrecisionScoreMet() ||
            App.API.FuzzySearch(FilterText, plugin.Description).IsSearchPrecisionScoreMet();
    }
}
