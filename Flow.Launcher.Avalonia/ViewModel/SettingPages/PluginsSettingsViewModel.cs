using System;
using Avalonia;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.Views.Controls;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using FluentAvalonia.UI.Controls;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class PluginsSettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Internationalization _i18n;

    public PluginsSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        
        LoadDisplayModes();
        LoadPlugins();
    }

    [ObservableProperty]
    private ObservableCollection<PluginItemViewModel> _plugins = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    public IEnumerable<PluginItemViewModel> FilteredPlugins => 
        string.IsNullOrWhiteSpace(SearchText) 
            ? Plugins 
            : Plugins.Where(p => 
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.ActionKeywordsText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            );

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredPlugins));

    private void LoadPlugins()
    {
        var allPlugins = PluginManager.AllPlugins;
        foreach (var plugin in allPlugins.OrderBy(p => p.Metadata.Disabled).ThenBy(p => p.Metadata.Name))
        {
            Plugins.Add(new PluginItemViewModel(plugin, _settings));
        }
    }

    #region Display Mode

    public enum DisplayMode
    {
        OnOff,
        Priority,
        SearchDelay,
        HomeOnOff
    }

    public class DisplayModeItem
    {
        public DisplayMode Value { get; }
        public string Display { get; }

        public DisplayModeItem(DisplayMode value, string display)
        {
            Value = value;
            Display = display;
        }
    }

    [ObservableProperty]
    private List<DisplayModeItem> _displayModes = new();

    [ObservableProperty]
    private DisplayModeItem? _selectedDisplayModeItem;

    partial void OnSelectedDisplayModeItemChanged(DisplayModeItem? value)
    {
        if (value != null)
            UpdateDisplayModeFlags(value.Value);
    }

    [ObservableProperty]
    private bool _isOnOffSelected = true;

    [ObservableProperty]
    private bool _isPrioritySelected;

    [ObservableProperty]
    private bool _isSearchDelaySelected;

    [ObservableProperty]
    private bool _isHomeOnOffSelected;

    private void LoadDisplayModes()
    {
        DisplayModes = new List<DisplayModeItem>
        {
            new(DisplayMode.OnOff, _i18n.GetTranslation("pluginDisplayOnOff")),
            new(DisplayMode.Priority, _i18n.GetTranslation("pluginDisplayPriority")),
            new(DisplayMode.SearchDelay, _i18n.GetTranslation("pluginDisplaySearchDelay")),
            new(DisplayMode.HomeOnOff, _i18n.GetTranslation("pluginDisplayHomeOnOff"))
        };
        
        // Set default
        SelectedDisplayModeItem = DisplayModes[0];
    }

    private void UpdateDisplayModeFlags(DisplayMode mode)
    {
        IsOnOffSelected = mode == DisplayMode.OnOff;
        IsPrioritySelected = mode == DisplayMode.Priority;
        IsSearchDelaySelected = mode == DisplayMode.SearchDelay;
        IsHomeOnOffSelected = mode == DisplayMode.HomeOnOff;
    }

    #endregion

    [RelayCommand]
    private async Task OpenHelper(Control source)
    {
        var helpDialog = new ContentDialog
        {
            Title = _i18n.GetTranslation("flowlauncher_settings"),
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = _i18n.GetTranslation("priority"),
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = _i18n.GetTranslation("priority_tips"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = _i18n.GetTranslation("searchDelay"),
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 10, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = _i18n.GetTranslation("searchDelayTimeTips"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = _i18n.GetTranslation("homeTitle"),
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 10, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = _i18n.GetTranslation("homeTips"),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            PrimaryButtonText = _i18n.GetTranslation("commonOK"),
            CloseButtonText = null
        };

        await helpDialog.ShowAsync();
    }
}

public partial class PluginItemViewModel : ObservableObject
{
    private readonly PluginPair _plugin;
    private readonly Settings _settings;
    private readonly ISettingProvider? _settingProvider;
    private readonly Internationalization _i18n;

    public PluginItemViewModel(PluginPair plugin, Settings settings)
    {
        _plugin = plugin;
        _settings = settings;
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        
        PluginSettingsObject = _settings.PluginSettings.GetPluginSettings(plugin.Metadata.ID);

        // Initialize settings provider
        if (plugin.Plugin is ISettingProvider settingProvider)
        {
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

        // Initialize Avalonia settings if available
        if (HasSettings && _settingProvider != null)
        {
            try
            {
                AvaloniaSettingControl = _settingProvider.CreateSettingPanelAvalonia();
                HasNativeAvaloniaSettings = AvaloniaSettingControl != null;
            }
            catch (Exception ex)
            {
                Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginItemViewModel), $"Failed to create Avalonia settings for {Name}", ex);
            }
        }

        // Listen to metadata changes
        _plugin.Metadata.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PluginMetadata.AvgQueryTime))
                OnPropertyChanged(nameof(QueryTime));
            if (args.PropertyName == nameof(PluginMetadata.ActionKeywords))
                OnPropertyChanged(nameof(ActionKeywordsText));
        };
        
        _ = LoadIconAsync();
    }

    public Infrastructure.UserSettings.Plugin PluginSettingsObject { get; }

    private async Task LoadIconAsync()
    {
        Icon = await Flow.Launcher.Avalonia.Helper.ImageLoader.LoadAsync(_plugin.Metadata.IcoPath);
    }

    [ObservableProperty]
    private IImage? _icon;

    [ObservableProperty]
    private bool _hasSettings;

    [ObservableProperty]
    private bool _hasNativeAvaloniaSettings;

    /// <summary>
    /// True if plugin has settings but only WPF settings (no native Avalonia)
    /// </summary>
    public bool HasWpfOnlySettings => HasSettings && !HasNativeAvaloniaSettings;

    [ObservableProperty]
    private Control? _avaloniaSettingControl;

    [ObservableProperty]
    private bool _isExpanded;

    public string Name => _plugin.Metadata.Name;
    public string Description => _plugin.Metadata.Description;
    public string Author => _plugin.Metadata.Author;
    public string Version => _plugin.Metadata.Version;
    public string IconPath => _plugin.Metadata.IcoPath;
    public string ID => _plugin.Metadata.ID;

    public string ActionKeywordsText => string.Join(Query.ActionKeywordSeparator, _plugin.Metadata.ActionKeywords);
    
    public string InitTime => $"{_plugin.Metadata.InitTime}ms";
    public string QueryTime => $"{_plugin.Metadata.AvgQueryTime}ms";

    public bool IsDisabled
    {
        get => _plugin.Metadata.Disabled;
        set
        {
            if (_plugin.Metadata.Disabled != value)
            {
                _plugin.Metadata.Disabled = value;
                PluginSettingsObject.Disabled = value;
                OnPropertyChanged();
                // Also update the inverse property for binding convenience
                OnPropertyChanged(nameof(PluginState));
            }
        }
    }

    public bool PluginState
    {
        get => !IsDisabled;
        set => IsDisabled = !value;
    }

    public bool PluginHomeState
    {
        get => !_plugin.Metadata.HomeDisabled;
        set
        {
            if (_plugin.Metadata.HomeDisabled != !value)
            {
                _plugin.Metadata.HomeDisabled = !value;
                PluginSettingsObject.HomeDisabled = !value;
                OnPropertyChanged();
            }
        }
    }

    public int Priority
    {
        get => _plugin.Metadata.Priority;
        set
        {
            if (_plugin.Metadata.Priority != value)
            {
                _plugin.Metadata.Priority = value;
                PluginSettingsObject.Priority = value;
                OnPropertyChanged();
            }
        }
    }

    public double PluginSearchDelayTime
    {
        get => _plugin.Metadata.SearchDelayTime == null ? double.NaN : _plugin.Metadata.SearchDelayTime.Value;
        set
        {
            if (double.IsNaN(value))
            {
                _plugin.Metadata.SearchDelayTime = null;
                PluginSettingsObject.SearchDelayTime = null;
            }
            else
            {
                _plugin.Metadata.SearchDelayTime = (int)value;
                PluginSettingsObject.SearchDelayTime = (int)value;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SearchDelayTimeText));
        }
    }

    public string SearchDelayTimeText => _plugin.Metadata.SearchDelayTime == null ?
        _i18n.GetTranslation("default") :
        _i18n.GetTranslation($"SearchDelayTime{_plugin.Metadata.SearchDelayTime}");

    public bool SearchDelayEnabled => _settings.SearchQueryResultsWithDelay;
    public string DefaultSearchDelay => _settings.SearchDelayTime.ToString();
    public bool HomeEnabled => _settings.ShowHomePage && PluginManager.IsHomePlugin(_plugin.Metadata.ID);

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
            // Create the WPF settings panel and show in a standalone WPF window
            var settingsControl = _settingProvider.CreateSettingPanel();
            if (settingsControl != null)
            {
                WpfSettingsWindow.Show(settingsControl, Name);
            }
        }
        catch (Exception ex)
        {
            Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginItemViewModel), $"Failed to open settings for {Name}", ex);
        }
    }

    [RelayCommand]
    private void OpenPluginDirectory()
    {
        var directory = _plugin.Metadata.PluginDirectory;
        if (!string.IsNullOrEmpty(directory))
            App.API.OpenDirectory(directory);
    }

    [RelayCommand]
    private void OpenSourceCodeLink()
    {
        if (!string.IsNullOrEmpty(_plugin.Metadata.Website))
            App.API.OpenUrl(_plugin.Metadata.Website);
    }

    [RelayCommand]
    private async Task OpenDeletePluginWindow()
    {
        // We need to implement a dialog for confirmation
        var dialog = new ContentDialog
        {
            Title = _i18n.GetTranslation("plugin_uninstall_title"),
            Content = string.Format(_i18n.GetTranslation("plugin_uninstall_content"), Name),
            PrimaryButtonText = _i18n.GetTranslation("yes"),
            CloseButtonText = _i18n.GetTranslation("no")
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
             await PluginInstaller.UninstallPluginAndCheckRestartAsync(_plugin.Metadata);
        }
    }

    [RelayCommand]
    private async Task SetActionKeywords()
    {
        // Simple dialog to edit keywords
        var textBox = new TextBox
        {
            Text = ActionKeywordsText,
            AcceptsReturn = false
        };

        var dialog = new ContentDialog
        {
            Title = _i18n.GetTranslation("actionKeywords"),
            Content = new StackPanel
            {
                Spacing = 10,
                Children = 
                {
                    new TextBlock { Text = _i18n.GetTranslation("actionKeywordsDescription") },
                    textBox
                }
            },
            PrimaryButtonText = _i18n.GetTranslation("done"),
            CloseButtonText = _i18n.GetTranslation("cancel")
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newKeywords = textBox.Text?.Split(Query.ActionKeywordSeparator, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();
            if (newKeywords != null)
            {
                // Validate?
                // For now just update
                _plugin.Metadata.ActionKeywords = newKeywords;
                PluginSettingsObject.ActionKeywords = newKeywords;
                OnPropertyChanged(nameof(ActionKeywordsText));
            }
        }
    }
}
