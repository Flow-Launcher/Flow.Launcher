using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using Flow.Launcher.Avalonia.Views.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaControl = Avalonia.Controls.Control;

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
    private readonly ISettingProvider? _settingProvider;

    public PluginItemViewModel(PluginPair plugin)
    {
        _plugin = plugin;

        // Check if plugin has settings - for JsonRPC plugins, also check NeedCreateSettingPanel()
        if (plugin.Plugin is ISettingProvider settingProvider)
        {
            // JsonRPC plugins may not have settings even if they implement ISettingProvider
            if (plugin.Plugin is JsonRPCPluginBase jsonRpcPlugin)
            {
                if (jsonRpcPlugin.NeedCreateSettingPanel())
                {
                    _settingProvider = settingProvider;
                    HasSettings = true;
                }
            }
            else
            {
                _settingProvider = settingProvider;
                HasSettings = true;
            }
        }

        if (HasSettings && _settingProvider != null)
        {
            try
            {
                System.Console.WriteLine($"Checking Avalonia settings for {Name}");
                AvaloniaSettingControl = _settingProvider.CreateSettingPanelAvalonia();
                HasNativeAvaloniaSettings = AvaloniaSettingControl != null;
                System.Console.WriteLine($"Avalonia settings for {Name}: {HasNativeAvaloniaSettings}");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to create Avalonia settings for {Name}: {ex}");
                Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginItemViewModel), $"Failed to create Avalonia settings for {Name}", ex);
            }
        }
    }

    [ObservableProperty]
    private bool _hasSettings;

    [ObservableProperty]
    private bool _hasNativeAvaloniaSettings;

    [ObservableProperty]
    private AvaloniaControl? _avaloniaSettingControl;

    [ObservableProperty]
    private bool _isExpanded;

    [RelayCommand]
    private void OpenSettings()
    {
        if (_settingProvider == null) return;

        if (HasNativeAvaloniaSettings)
        {
            IsExpanded = !IsExpanded;
            return;
        }

        try
        {
            // Create the WPF settings panel on demand
            var settingsControl = _settingProvider.CreateSettingPanel();
            if (settingsControl != null)
            {
                WpfSettingsWindow.Show(settingsControl, Name);
            }
        }
        catch (System.Exception ex)
        {
            // Log the error so we can diagnose issues
            System.Diagnostics.Debug.WriteLine($"Failed to open settings for {Name}: {ex}");
            Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginItemViewModel), $"Failed to open settings for {Name}", ex);
        }
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
