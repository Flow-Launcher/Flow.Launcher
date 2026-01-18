using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class PluginsSettingsViewModel : ObservableObject
{
    public PluginsSettingsViewModel()
    {
        LoadPlugins();
    }

    [ObservableProperty]
    private ObservableCollection<PluginItemViewModel> _plugins = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    public IEnumerable<PluginItemViewModel> FilteredPlugins => 
        string.IsNullOrWhiteSpace(SearchText) 
            ? Plugins 
            : Plugins.Where(p => p.Name.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase));

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredPlugins));

    private void LoadPlugins()
    {
        var allPlugins = PluginManager.AllPlugins;
        foreach (var plugin in allPlugins.OrderBy(p => p.Metadata.Name))
        {
            Plugins.Add(new PluginItemViewModel(plugin));
        }
    }
}

public partial class PluginItemViewModel : ObservableObject
{
    private readonly PluginPair _plugin;

    public PluginItemViewModel(PluginPair plugin)
    {
        _plugin = plugin;
    }

    public string Name => _plugin.Metadata.Name;
    public string Description => _plugin.Metadata.Description;
    public string Author => _plugin.Metadata.Author;
    public string Version => _plugin.Metadata.Version;
    public string IconPath => _plugin.Metadata.IcoPath;

    public bool IsDisabled
    {
        get => _plugin.Metadata.Disabled;
        set
        {
            if (_plugin.Metadata.Disabled != value)
            {
                _plugin.Metadata.Disabled = value;
                OnPropertyChanged();
            }
        }
    }
}
