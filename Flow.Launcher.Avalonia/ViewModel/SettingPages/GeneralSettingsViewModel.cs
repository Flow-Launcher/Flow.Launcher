using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaI18n = Flow.Launcher.Avalonia.Resource.Internationalization;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly Flow.Launcher.Infrastructure.UserSettings.Settings _settings;
    private readonly AvaloniaI18n _i18n;

    public GeneralSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Flow.Launcher.Infrastructure.UserSettings.Settings>();
        _i18n = Ioc.Default.GetRequiredService<AvaloniaI18n>();
        
        LoadLanguages();
        LoadSearchWindowOptions();
        LoadSearchPrecisionOptions();
        LoadLastQueryModeOptions();
    }

    // Direct access to settings for bindings
    public Flow.Launcher.Infrastructure.UserSettings.Settings Settings => _settings;

    #region Languages

    [ObservableProperty]
    private List<Language> _languages = new();

    public Language? SelectedLanguage
    {
        get => Languages.FirstOrDefault(l => l.LanguageCode == _settings.Language);
        set
        {
            if (value != null && value.LanguageCode != _settings.Language)
            {
                _settings.Language = value.LanguageCode;
                _i18n.ChangeLanguage(value.LanguageCode);
                OnPropertyChanged();
                UpdateLabels();
            }
        }
    }

    private void UpdateLabels()
    {
        SearchWindowScreens.ForEach(x => x.UpdateLabels());
        SearchWindowAligns.ForEach(x => x.UpdateLabels());
        SearchPrecisionScores.ForEach(x => x.UpdateLabels());
        LastQueryModes.ForEach(x => x.UpdateLabels());
    }


    #endregion

    #region Startup Settings

    public bool StartOnStartup
    {
        get => _settings.StartFlowLauncherOnSystemStartup;
        set
        {
            _settings.StartFlowLauncherOnSystemStartup = value;
            OnPropertyChanged();
        }
    }

    public bool UseLogonTaskForStartup
    {
        get => _settings.UseLogonTaskForStartup;
        set
        {
            _settings.UseLogonTaskForStartup = value;
            OnPropertyChanged();
        }
    }

    public bool HideOnStartup
    {
        get => _settings.HideOnStartup;
        set
        {
            _settings.HideOnStartup = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Behavior Settings

    public bool HideWhenDeactivated
    {
        get => _settings.HideWhenDeactivated;
        set
        {
            _settings.HideWhenDeactivated = value;
            OnPropertyChanged();
        }
    }

    public bool HideNotifyIcon
    {
        get => _settings.HideNotifyIcon;
        set
        {
            _settings.HideNotifyIcon = value;
            OnPropertyChanged();
        }
    }

    public bool ShowAtTopmost
    {
        get => _settings.ShowAtTopmost;
        set
        {
            _settings.ShowAtTopmost = value;
            OnPropertyChanged();
        }
    }

    public bool IgnoreHotkeysOnFullscreen
    {
        get => _settings.IgnoreHotkeysOnFullscreen;
        set
        {
            _settings.IgnoreHotkeysOnFullscreen = value;
            OnPropertyChanged();
        }
    }

    public bool AlwaysPreview
    {
        get => _settings.AlwaysPreview;
        set
        {
            _settings.AlwaysPreview = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Update Settings

    public bool AutoUpdates
    {
        get => _settings.AutoUpdates;
        set
        {
            _settings.AutoUpdates = value;
            OnPropertyChanged();
        }
    }

    public bool AutoUpdatePlugins
    {
        get => _settings.AutoUpdatePlugins;
        set
        {
            _settings.AutoUpdatePlugins = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Search Window Position

    [ObservableProperty]
    private List<DropdownDataGeneric<SearchWindowScreens>> _searchWindowScreens = new();

    public SearchWindowScreens SelectedSearchWindowScreen
    {
        get => _settings.SearchWindowScreen;
        set
        {
            _settings.SearchWindowScreen = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private List<DropdownDataGeneric<SearchWindowAligns>> _searchWindowAligns = new();

    public SearchWindowAligns SelectedSearchWindowAlign
    {
        get => _settings.SearchWindowAlign;
        set
        {
            _settings.SearchWindowAlign = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Query Settings

    [ObservableProperty]
    private List<DropdownDataGeneric<SearchPrecisionScore>> _searchPrecisionScores = new();

    public SearchPrecisionScore SelectedSearchPrecision
    {
        get => _settings.QuerySearchPrecision;
        set
        {
            _settings.QuerySearchPrecision = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private List<DropdownDataGeneric<LastQueryMode>> _lastQueryModes = new();


    public LastQueryMode SelectedLastQueryMode
    {
        get => _settings.LastQueryMode;
        set
        {
            _settings.LastQueryMode = value;
            OnPropertyChanged();
        }
    }

    public bool SearchQueryResultsWithDelay
    {
        get => _settings.SearchQueryResultsWithDelay;
        set
        {
            _settings.SearchQueryResultsWithDelay = value;
            OnPropertyChanged();
        }
    }

    public int SearchDelayTime
    {
        get => _settings.SearchDelayTime;
        set
        {
            _settings.SearchDelayTime = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Home Page Settings

    public bool ShowHomePage
    {
        get => _settings.ShowHomePage;
        set
        {
            _settings.ShowHomePage = value;
            OnPropertyChanged();
        }
    }

    public bool ShowHistoryResultsForHomePage
    {
        get => _settings.ShowHistoryResultsForHomePage;
        set
        {
            _settings.ShowHistoryResultsForHomePage = value;
            OnPropertyChanged();
        }
    }

    public int MaxHistoryResultsToShow
    {
        get => _settings.MaxHistoryResultsToShowForHomePage;
        set
        {
            _settings.MaxHistoryResultsToShowForHomePage = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Miscellaneous Settings

    public bool AutoRestartAfterChanging
    {
        get => _settings.AutoRestartAfterChanging;
        set
        {
            _settings.AutoRestartAfterChanging = value;
            OnPropertyChanged();
        }
    }

    public bool ShowUnknownSourceWarning
    {
        get => _settings.ShowUnknownSourceWarning;
        set
        {
            _settings.ShowUnknownSourceWarning = value;
            OnPropertyChanged();
        }
    }

    public bool AlwaysStartEn
    {
        get => _settings.AlwaysStartEn;
        set
        {
            _settings.AlwaysStartEn = value;
            OnPropertyChanged();
        }
    }

    public bool ShouldUsePinyin
    {
        get => _settings.ShouldUsePinyin;
        set
        {
            _settings.ShouldUsePinyin = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Paths

    public string PythonPath => _settings.PluginSettings.PythonExecutablePath ?? "Not set";
    public string NodePath => _settings.PluginSettings.NodeExecutablePath ?? "Not set";

    [RelayCommand]
    private async Task SelectPython()
    {
        // TODO: Implement file picker
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SelectNode()
    {
        // TODO: Implement file picker
        await Task.CompletedTask;
    }

    #endregion

    #region Load Options

    private void LoadLanguages()
    {
        // Minimal set of languages for now, can be expanded by loading from directory later
        Languages = new List<Language>
        {
            new Language("en", "English"),
            new Language("zh-cn", "中文 (简体)"),
            new Language("zh-tw", "中文 (繁體)"),
            new Language("ko", "한국어"),
            new Language("ja", "日本語")
        };
    }

    private void LoadSearchWindowOptions()
    {
        SearchWindowScreens = DropdownDataGeneric<SearchWindowScreens>.GetEnumData("SearchWindowScreen");
        SearchWindowAligns = DropdownDataGeneric<SearchWindowAligns>.GetEnumData("SearchWindowAlign");
    }

    private void LoadSearchPrecisionOptions()
    {
        SearchPrecisionScores = DropdownDataGeneric<SearchPrecisionScore>.GetEnumData("SearchPrecision");
    }

    private void LoadLastQueryModeOptions()
    {
        LastQueryModes = DropdownDataGeneric<LastQueryMode>.GetEnumData("LastQuery");
    }

    #endregion
}

