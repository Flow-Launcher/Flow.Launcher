using System;
using System.ComponentModel;
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
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using FluentAvalonia.UI.Controls;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class PluginsSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Settings _settings;
    private readonly Internationalization _i18n;
    private readonly PropertyChangedEventHandler _settingsPropertyChangedHandler;

    public PluginsSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        
        LoadDisplayModes();
        LoadPlugins();

        _settingsPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.Language))
            {
                foreach (var item in DisplayModes)
                {
                    item.UpdateLabels();
                }
            }
        };
        _settings.PropertyChanged += _settingsPropertyChangedHandler;

        _ = CheckForUpdatesSilentlyAsync();
    }

    [ObservableProperty]
    private ObservableCollection<PluginItemViewModel> _plugins = new();


    [ObservableProperty]
    private string _searchText = string.Empty;

    public IEnumerable<PluginItemViewModel> FilteredPlugins =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Plugins
            : Plugins.Where(p =>
                App.API.FuzzySearch(SearchText, p.Name).IsSearchPrecisionScoreMet() ||
                App.API.FuzzySearch(SearchText, p.Description).IsSearchPrecisionScoreMet() ||
                p.ActionKeywordsText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            );

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredPlugins));

    /// <summary>
    /// Filters the list down to the given plugin and expands it.
    /// </summary>
    public void FocusPlugin(string pluginId)
    {
        var target = Plugins.FirstOrDefault(p => string.Equals(p.ID, pluginId, StringComparison.OrdinalIgnoreCase));
        if (target == null) return;

        foreach (var plugin in Plugins)
        {
            plugin.IsExpanded = false;
        }

        SearchText = target.Name;
        target.IsExpanded = true;
    }

    private void LoadPlugins()
    {
        var allPlugins = PluginManager.GetAllLoadedPlugins();
        var mode = SelectedDisplayModeItem?.Value ?? DisplayMode.OnOff;
        foreach (var plugin in allPlugins.OrderBy(p => p.Metadata.Disabled).ThenBy(p => p.Metadata.Name))
        {
            var item = new PluginItemViewModel(plugin, _settings) { Mode = mode };
            item.UpdateStateChanged += OnItemUpdateStateChanged;
            Plugins.Add(item);
        }
    }

    #region Updates

    public int AvailableUpdatesCount => Plugins.Count(p => p.HasUpdate);
    public bool HasAvailableUpdates => AvailableUpdatesCount > 0;

    private void OnItemUpdateStateChanged()
    {
        OnPropertyChanged(nameof(AvailableUpdatesCount));
        OnPropertyChanged(nameof(HasAvailableUpdates));
    }

    private bool _updatesChecked;

    private async Task CheckForUpdatesSilentlyAsync()
    {
        if (_updatesChecked) return;
        _updatesChecked = true;

        try
        {
            await CheckForUpdatesCoreAsync();
        }
        catch (Exception e)
        {
            _updatesChecked = false;
            App.API.LogException(nameof(PluginsSettingsViewModel), "Failed to check for plugin updates", e);
        }
    }

    [RelayCommand]
    private Task CheckPluginUpdates() => CheckForUpdatesCoreAsync();

    private async Task CheckForUpdatesCoreAsync()
    {
        await App.API.UpdatePluginManifestAsync();
        var manifest = App.API.GetPluginManifest();

        foreach (var item in Plugins)
        {
            var manifestPlugin = manifest.FirstOrDefault(p => p.ID == item.ID);
            if (manifestPlugin != null &&
                System.Version.TryParse(item.RawVersion, out var current) &&
                System.Version.TryParse(manifestPlugin.Version, out var latest) &&
                current < latest &&
                !App.API.PluginModified(item.ID))
            {
                item.UpdateInfo = new PluginUpdateInfo
                {
                    ID = item.ID,
                    Name = item.Name,
                    Author = item.Author,
                    CurrentVersion = item.RawVersion,
                    NewVersion = manifestPlugin.Version,
                    IcoPath = item.IconPath,
                    PluginExistingMetadata = item.Metadata,
                    PluginNewUserPlugin = manifestPlugin
                };
            }
            else
            {
                item.UpdateInfo = null;
            }
        }

        OnItemUpdateStateChanged();
    }

    [RelayCommand]
    private async Task UpdateAllPlugins()
    {
        var updates = Plugins.Where(p => p.UpdateInfo != null).Select(p => p.UpdateInfo!).ToList();
        if (updates.Count == 0) return;

        if (global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var owner = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        if (owner == null) return;

        var window = new Views.Dialogs.PluginUpdateWindow(updates);
        var updated = await window.ShowDialog<bool>(owner);
        if (updated)
        {
            await CheckForUpdatesCoreAsync();
        }
    }

    #endregion
    public void Dispose()
    {
        _settings.PropertyChanged -= _settingsPropertyChangedHandler;

        foreach (var plugin in Plugins)
        {
            plugin.Dispose();
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

    public partial class DisplayModeItem : ObservableObject
    {
        private readonly Internationalization _i18n;
        public DisplayMode Value { get; }

        [ObservableProperty]
        private string _display;

        public DisplayModeItem(DisplayMode value, Internationalization i18n)
        {
            Value = value;
            _i18n = i18n;
            UpdateLabels();
        }

        public void UpdateLabels()
        {
            Display = Value switch
            {
                DisplayMode.OnOff => _i18n.GetTranslation("DisplayModeOnOff"),
                DisplayMode.Priority => _i18n.GetTranslation("DisplayModePriority"),
                DisplayMode.SearchDelay => _i18n.GetTranslation("DisplayModeSearchDelay"),
                DisplayMode.HomeOnOff => _i18n.GetTranslation("DisplayModeHomeOnOff"),
                _ => Value.ToString()
            };
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
            new(DisplayMode.OnOff, _i18n),
            new(DisplayMode.Priority, _i18n),
            new(DisplayMode.SearchDelay, _i18n),
            new(DisplayMode.HomeOnOff, _i18n)
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

        foreach (var plugin in Plugins)
        {
            plugin.Mode = mode;
        }
    }

    #endregion

    [RelayCommand]
    private async Task OpenHelper(Control source)
    {
        var helpDialog = new FAContentDialog
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

public partial class PluginItemViewModel : ObservableObject, IDisposable
{
    private readonly PluginPair _plugin;
    private readonly Settings _settings;
    private readonly ISettingProvider? _settingProvider;
    private readonly Internationalization _i18n;
    private readonly PropertyChangedEventHandler _metadataChangedHandler;
    private readonly PropertyChangedEventHandler _settingsChangedHandler;

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

        // The Avalonia settings panel is created lazily on first expand; assume it exists until creation says otherwise.
        HasNativeAvaloniaSettings = HasSettings;

        // Listen to metadata changes
        _metadataChangedHandler = (_, args) =>
        {
            if (args.PropertyName == nameof(PluginMetadata.AvgQueryTime))
                OnPropertyChanged(nameof(QueryTime));
            if (args.PropertyName == nameof(PluginMetadata.ActionKeywords))
                OnPropertyChanged(nameof(ActionKeywordsText));
        };
        _plugin.Metadata.PropertyChanged += _metadataChangedHandler;

        _settingsChangedHandler = (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(Settings.SearchQueryResultsWithDelay):
                    OnPropertyChanged(nameof(SearchDelayEnabled));
                    break;
                case nameof(Settings.SearchDelayTime):
                    OnPropertyChanged(nameof(DefaultSearchDelay));
                    break;
                case nameof(Settings.ShowHomePage):
                    OnPropertyChanged(nameof(HomeEnabled));
                    break;
            }
        };
        _settings.PropertyChanged += _settingsChangedHandler;
        
        _ = LoadIconAsync();
    }

    public Infrastructure.UserSettings.Plugin PluginSettingsObject { get; }

    private async Task LoadIconAsync()
    {
        Icon = await Flow.Launcher.Avalonia.Helper.ImageLoader.LoadAsync(_plugin.Metadata.IcoPath);
    }
    public void Dispose()
    {
        _plugin.Metadata.PropertyChanged -= _metadataChangedHandler;
        _settings.PropertyChanged -= _settingsChangedHandler;
    }

    private void EnsureAvaloniaSettingControl()
    {
        if (AvaloniaSettingControl != null || !HasNativeAvaloniaSettings || _settingProvider == null)
            return;

        try
        {
            AvaloniaSettingControl = _settingProvider.CreateSettingPanelAvalonia() as Control;
        }
        catch (Exception ex)
        {
            Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginItemViewModel), $"Failed to create Avalonia settings for {Name}", ex);
            AvaloniaSettingControl = CreateErrorSettingPanel(string.Format(
                _i18n.GetTranslation("errorCreatingSettingPanel"), Name, Environment.NewLine, ex.Message));
        }

        if (AvaloniaSettingControl == null)
            HasNativeAvaloniaSettings = false;
    }

    private static Control CreateErrorSettingPanel(string text) => new TextBox
    {
        Text = text,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
        Foreground = Brushes.IndianRed
    };

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
            EnsureAvaloniaSettingControl();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnOffSelected))]
    [NotifyPropertyChangedFor(nameof(IsPrioritySelected))]
    [NotifyPropertyChangedFor(nameof(IsSearchDelaySelected))]
    [NotifyPropertyChangedFor(nameof(IsHomeOnOffSelected))]
    private PluginsSettingsViewModel.DisplayMode _mode;

    public bool IsOnOffSelected => Mode == PluginsSettingsViewModel.DisplayMode.OnOff;
    public bool IsPrioritySelected => Mode == PluginsSettingsViewModel.DisplayMode.Priority;
    public bool IsSearchDelaySelected => Mode == PluginsSettingsViewModel.DisplayMode.SearchDelay;
    public bool IsHomeOnOffSelected => Mode == PluginsSettingsViewModel.DisplayMode.HomeOnOff;

    [ObservableProperty]
    private IImage? _icon;

    [ObservableProperty]
    private bool _hasSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWpfOnlySettings))]
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
            value = Math.Clamp(value, -999, 999);
            if (_plugin.Metadata.Priority != value)
            {
                _plugin.Metadata.Priority = value;
                PluginSettingsObject.Priority = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Number box binding for <see cref="Priority"/>: empty input becomes 0 and the value is clamped to ±999.
    /// </summary>
    public double PriorityInput
    {
        get => Priority;
        set
        {
            Priority = double.IsNaN(value) ? 0 : (int)Math.Round(value);
            // Always refresh so the box shows the normalized value even when Priority did not change.
            OnPropertyChanged();
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
                var delay = Math.Clamp((int)value, 0, 1000);
                _plugin.Metadata.SearchDelayTime = delay;
                PluginSettingsObject.SearchDelayTime = delay;
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
    public bool HideActionKeywordPanel => _plugin.Metadata.HideActionKeywordPanel;
    public bool ShowActionKeywordPanel => !HideActionKeywordPanel;

    public PluginMetadata Metadata => _plugin.Metadata;
    public string RawVersion => _plugin.Metadata.Version;

    private PluginUpdateInfo? _updateInfo;

    public event Action? UpdateStateChanged;

    public PluginUpdateInfo? UpdateInfo
    {
        get => _updateInfo;
        set
        {
            if (_updateInfo == value) return;
            _updateInfo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUpdate));
            OnPropertyChanged(nameof(UpdateVersionText));
            UpdateStateChanged?.Invoke();
        }
    }

    public bool HasUpdate => _updateInfo != null;

    /// <summary>"old → new" version text shown next to the update button.</summary>
    public string UpdateVersionText => _updateInfo == null ? string.Empty : $"v{_updateInfo.CurrentVersion} → v{_updateInfo.NewVersion}";

    [RelayCommand]
    private async Task UpdatePlugin()
    {
        if (_updateInfo == null) return;

        var success = await PluginInstaller.UpdatePluginAndCheckRestartAsync(
            _updateInfo.PluginNewUserPlugin, _updateInfo.PluginExistingMetadata);
        if (success)
        {
            UpdateInfo = null;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (_settingProvider == null) return;

        if (HasNativeAvaloniaSettings)
        {
            IsExpanded = !IsExpanded;
            return;
        }

        OpenWpfSettingsWindow();
    }

    // Legacy WPF settings panels can only be hosted on Windows; implemented in PluginsSettingsViewModel.Windows.cs.
    partial void OpenWpfSettingsWindow();

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
        var dialog = new FAContentDialog
        {
            Title = _i18n.GetTranslation("plugin_uninstall_title"),
            Content = string.Format(_i18n.GetTranslation("plugin_uninstall_content"), Name),
            PrimaryButtonText = _i18n.GetTranslation("yes"),
            CloseButtonText = _i18n.GetTranslation("no")
        };

        var result = await dialog.ShowAsync();
        if (result == FAContentDialogResult.Primary)
        {
             await PluginInstaller.UninstallPluginAndCheckRestartAsync(_plugin.Metadata);
        }
    }

    [RelayCommand]
    private async Task SetActionKeywords()
    {
        var textBox = new TextBox
        {
            Text = ActionKeywordsText,
            AcceptsReturn = false
        };
        var errorText = new TextBlock
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        var dialog = new FAContentDialog
        {
            Title = _i18n.GetTranslation("actionKeywords"),
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = _i18n.GetTranslation("actionKeywordsDescription") },
                    textBox,
                    errorText
                }
            },
            PrimaryButtonText = _i18n.GetTranslation("done"),
            CloseButtonText = _i18n.GetTranslation("cancel")
        };

        var oldKeywords = _plugin.Metadata.ActionKeywords;
        List<string> newKeywords = [];

        dialog.PrimaryButtonClick += (_, args) =>
        {
            newKeywords = textBox.Text?
                .Split(Query.ActionKeywordSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? [];

            if (newKeywords.Count == 0)
            {
                newKeywords.Add(Query.GlobalPluginWildcardSign);
            }

            string? error = null;
            var added = newKeywords.Except(oldKeywords, StringComparer.Ordinal).ToList();
            var removed = oldKeywords.Except(newKeywords, StringComparer.Ordinal).ToList();
            if (added.Count == 0 && removed.Count == 0)
            {
                error = _i18n.GetTranslation("newActionKeywordsSameAsOld");
            }
            else if (added.Any(App.API.ActionKeywordAssigned))
            {
                error = _i18n.GetTranslation("newActionKeywordsHasBeenAssigned");
            }

            if (error != null)
            {
                errorText.Text = error;
                errorText.IsVisible = true;
                args.Cancel = true;
            }
        };

        var result = await dialog.ShowAsync();
        if (result == FAContentDialogResult.Primary)
        {
            var addedKeywords = newKeywords.Except(oldKeywords, StringComparer.Ordinal).ToList();
            var removedKeywords = oldKeywords.Except(newKeywords, StringComparer.Ordinal).ToList();

            var api = App.API;
            if (api == null)
            {
                return;
            }

            foreach (var keyword in removedKeywords)
            {
                api.RemoveActionKeyword(_plugin.Metadata.ID, keyword);
            }

            foreach (var keyword in addedKeywords)
            {
                api.AddActionKeyword(_plugin.Metadata.ID, keyword);
            }

            PluginSettingsObject.ActionKeywords = _plugin.Metadata.ActionKeywords.ToList();
            OnPropertyChanged(nameof(ActionKeywordsText));
        }
    }
}
