using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AvaloniaI18n = Flow.Launcher.Avalonia.Resource.Internationalization;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly Flow.Launcher.Infrastructure.UserSettings.Settings _settings;
    private readonly AvaloniaI18n _i18n;
    private readonly Updater _updater;
    private readonly Portable _portable;

    public GeneralSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Flow.Launcher.Infrastructure.UserSettings.Settings>();
        _i18n = Ioc.Default.GetRequiredService<AvaloniaI18n>();
        _updater = Ioc.Default.GetRequiredService<Updater>();
        _portable = Ioc.Default.GetRequiredService<Portable>();
        
        LoadLanguages();
        LoadSearchWindowOptions();
        LoadSearchPrecisionOptions();
        LoadLastQueryModeOptions();
        LoadHistoryStyleOptions();
        LoadDoublePinyinSchemas();
        LoadDialogJumpOptions();
    }

    // Direct access to settings for bindings
    public Flow.Launcher.Infrastructure.UserSettings.Settings Settings => _settings;

    public string Crowdin => Constant.CrowdinProjectUrl;

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
        DoublePinyinSchemas.ForEach(x => x.UpdateLabels());
        DialogJumpWindowPositions.ForEach(x => x.UpdateLabels());
        DialogJumpResultBehaviours.ForEach(x => x.UpdateLabels());
        DialogJumpFileResultBehaviours.ForEach(x => x.UpdateLabels());
        HistoryStyles.ForEach(x => x.UpdateLabel(_i18n));
    }


    #endregion

    #region Startup Settings

    public bool StartOnStartup
    {
        get => _settings.StartFlowLauncherOnSystemStartup;
        set
        {
            if (_settings.StartFlowLauncherOnSystemStartup == value)
            {
                return;
            }

            try
            {
                if (value)
                {
                    if (UseLogonTaskForStartup)
                    {
                        AutoStartup.ChangeToViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                else
                {
                    AutoStartup.DisableViaLogonTaskAndRegistry();
                }
            }
            catch (Exception e)
            {
                App.API?.ShowMsgError(Translator.GetString("setAutoStartFailed"), e.Message);
                return;
            }

            _settings.StartFlowLauncherOnSystemStartup = value;
            OnPropertyChanged();
        }
    }

    public bool UseLogonTaskForStartup
    {
        get => _settings.UseLogonTaskForStartup;
        set
        {
            if (_settings.UseLogonTaskForStartup == value)
            {
                return;
            }

            if (StartOnStartup)
            {
                try
                {
                    if (value)
                    {
                        AutoStartup.ChangeToViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                catch (Exception e)
                {
                    App.API?.ShowMsgError(Translator.GetString("setAutoStartFailed"), e.Message);
                    return;
                }
            }

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

            if (value)
            {
                _ = _updater.UpdateAppAsync(false);
            }

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
            OnPropertyChanged(nameof(IsCustomWindowScreen));
            OnPropertyChanged(nameof(IsSearchWindowAlignVisible));
        }
    }

    [ObservableProperty]
    private List<DropdownDataGeneric<SearchWindowAligns>> _searchWindowAligns = new();

    public List<int> ScreenNumbers => Enumerable.Range(1, GetScreenCount()).ToList();

    public SearchWindowAligns SelectedSearchWindowAlign
    {
        get => _settings.SearchWindowAlign;
        set
        {
            _settings.SearchWindowAlign = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomSearchWindowAlign));
        }
    }

    public int CustomScreenNumber
    {
        get => _settings.CustomScreenNumber;
        set
        {
            _settings.CustomScreenNumber = value;
            OnPropertyChanged();
        }
    }

    public double CustomWindowLeft
    {
        get => _settings.CustomWindowLeft;
        set
        {
            _settings.CustomWindowLeft = value;
            OnPropertyChanged();
        }
    }

    public double CustomWindowTop
    {
        get => _settings.CustomWindowTop;
        set
        {
            _settings.CustomWindowTop = value;
            OnPropertyChanged();
        }
    }

    public bool IsCustomWindowScreen => SelectedSearchWindowScreen == Flow.Launcher.Infrastructure.UserSettings.SearchWindowScreens.Custom;

    public bool IsSearchWindowAlignVisible => SelectedSearchWindowScreen != Flow.Launcher.Infrastructure.UserSettings.SearchWindowScreens.RememberLastLaunchLocation;

    public bool IsCustomSearchWindowAlign => SelectedSearchWindowAlign == Flow.Launcher.Infrastructure.UserSettings.SearchWindowAligns.Custom;

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

    [ObservableProperty]
    private List<HistoryStyleOption> _historyStyles = new();

    public HistoryStyleOption? SelectedHistoryStyle
    {
        get => HistoryStyles.FirstOrDefault(x => x.Value == _settings.HistoryStyle);
        set
        {
            if (value == null || _settings.HistoryStyle == value.Value)
            {
                return;
            }

            _settings.HistoryStyle = value.Value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Miscellaneous Settings

    public string SelectedFileManagerDisplayName => _settings.CustomExplorer.DisplayName;

    public string SelectedBrowserDisplayName => _settings.CustomBrowser.DisplayName;

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
            if (!value && UseDoublePinyin)
            {
                UseDoublePinyin = false;
            }

            _settings.ShouldUsePinyin = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDoublePinyinVisible));
        }
    }

    public bool ShowTaskbarWhenOpened
    {
        get => _settings.ShowTaskbarWhenInvoked;
        set
        {
            _settings.ShowTaskbarWhenInvoked = value;
            OnPropertyChanged();
        }
    }

    public bool PortableMode
    {
        get => DataLocation.PortableDataLocationInUse();
        set
        {
            if (DataLocation.PortableDataLocationInUse() == value)
            {
                return;
            }

            if (!_portable.CanUpdatePortability())
            {
                return;
            }

            if (value)
            {
                _portable.EnablePortableMode();
            }
            else
            {
                _portable.DisablePortableMode();
            }

            OnPropertyChanged();
        }
    }

    public bool EnableDialogJump
    {
        get => _settings.EnableDialogJump;
        set
        {
            if (_settings.EnableDialogJump == value)
            {
                return;
            }

            _settings.EnableDialogJump = value;
            DialogJump.SetupDialogJump(value);

            OnPropertyChanged();
        }
    }

    public bool ShowDialogJumpWindow
    {
        get => _settings.ShowDialogJumpWindow;
        set
        {
            _settings.ShowDialogJumpWindow = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private List<DropdownDataGeneric<DoublePinyinSchemas>> _doublePinyinSchemas = new();

    [ObservableProperty]
    private List<DropdownDataGeneric<DialogJumpWindowPositions>> _dialogJumpWindowPositions = new();

    [ObservableProperty]
    private List<DropdownDataGeneric<DialogJumpResultBehaviours>> _dialogJumpResultBehaviours = new();

    [ObservableProperty]
    private List<DropdownDataGeneric<DialogJumpFileResultBehaviours>> _dialogJumpFileResultBehaviours = new();

    public bool UseDoublePinyin
    {
        get => _settings.UseDoublePinyin;
        set
        {
            _settings.UseDoublePinyin = value;
            OnPropertyChanged();
        }
    }

    public bool IsDoublePinyinVisible => ShouldUsePinyin;

    public DoublePinyinSchemas SelectedDoublePinyinSchema
    {
        get => _settings.DoublePinyinSchema;
        set
        {
            _settings.DoublePinyinSchema = value;
            OnPropertyChanged();
        }
    }

    public DialogJumpWindowPositions SelectedDialogJumpWindowPosition
    {
        get => _settings.DialogJumpWindowPosition;
        set
        {
            _settings.DialogJumpWindowPosition = value;
            OnPropertyChanged();
        }
    }

    public DialogJumpResultBehaviours SelectedDialogJumpResultBehaviour
    {
        get => _settings.DialogJumpResultBehaviour;
        set
        {
            _settings.DialogJumpResultBehaviour = value;
            OnPropertyChanged();
        }
    }

    public DialogJumpFileResultBehaviours SelectedDialogJumpFileResultBehaviour
    {
        get => _settings.DialogJumpFileResultBehaviour;
        set
        {
            _settings.DialogJumpFileResultBehaviour = value;
            OnPropertyChanged();
        }
    }

    public bool KoreanIMERegistryKeyExists
    {
        get
        {
            var registryKeyExists = Win32Helper.IsKoreanIMEExist();
            var koreanLanguageInstalled = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().Any(lang => lang.Culture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase));
            var isWindows11 = Win32Helper.IsWindows11();
            return (isWindows11 && koreanLanguageInstalled) || registryKeyExists;
        }
    }

    public bool LegacyKoreanIMEEnabled
    {
        get => Win32Helper.IsLegacyKoreanIMEEnabled();
        set
        {
            if (Win32Helper.SetLegacyKoreanIMEEnabled(value))
            {
                OnPropertyChanged();
            }
            else
            {
                App.API?.ShowMsgError(_i18n.GetTranslation("KoreanImeSettingChangeFailTitle"), _i18n.GetTranslation("KoreanImeSettingChangeFailSubTitle"));
            }
        }
    }

    #endregion

    #region Paths

    public string PythonPath => _settings.PluginSettings.PythonExecutablePath ?? "Not set";
    public string NodePath => _settings.PluginSettings.NodeExecutablePath ?? "Not set";

    [RelayCommand]
    private async Task SelectPython()
    {
        var topLevel = TopLevel.GetTopLevel(global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _i18n.GetTranslation("selectPythonExecutable"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Python") { Patterns = ["pythonw.exe", "python.exe"] },
                FilePickerFileTypes.All
            ]
        });

        var selectedFile = files.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(selectedFile))
        {
            _settings.PluginSettings.PythonExecutablePath = selectedFile;
            OnPropertyChanged(nameof(PythonPath));
        }
    }

    [RelayCommand]
    private async Task SelectNode()
    {
        var topLevel = TopLevel.GetTopLevel(global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _i18n.GetTranslation("selectNodeExecutable"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Node") { Patterns = ["*.exe"] },
                FilePickerFileTypes.All
            ]
        });

        var selectedFile = files.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(selectedFile))
        {
            _settings.PluginSettings.NodeExecutablePath = selectedFile;
            OnPropertyChanged(nameof(NodePath));
        }
    }

    [RelayCommand]
    private async Task SelectFileManager()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var window = new SelectFileManagerWindow();
        var result = await window.ShowDialog<bool>(desktop.MainWindow);
        if (!result)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedFileManagerDisplayName));
    }

    [RelayCommand]
    private async Task SelectBrowser()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var window = new SelectBrowserWindow();
        var result = await window.ShowDialog<bool>(desktop.MainWindow);
        if (!result)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedBrowserDisplayName));
    }

    [RelayCommand]
    private void OpenImeSettings()
    {
        Win32Helper.OpenImeSettings();
    }

    [RelayCommand]
    private async Task OpenCrowdin()
    {
        App.API?.OpenUrl(Crowdin);
        await Task.CompletedTask;
    }

    #endregion

    #region Load Options

    private void LoadLanguages()
    {
        Languages = new List<Language>
        {
            new(Constant.SystemLanguageCode, "System"),
            new("en", "English"),
            new("zh-cn", "中文 (简体)"),
            new("zh-tw", "中文 (繁體)"),
            new("ko", "한국어"),
            new("ja", "日本語")
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

    private void LoadHistoryStyleOptions()
    {
        HistoryStyles =
        [
            new HistoryStyleOption(HistoryStyle.Query, _i18n.GetTranslation("queryHistory")),
            new HistoryStyleOption(HistoryStyle.LastOpened, _i18n.GetTranslation("executedHistory"))
        ];
    }

    private void LoadDoublePinyinSchemas()
    {
        DoublePinyinSchemas = DropdownDataGeneric<DoublePinyinSchemas>.GetEnumData("DoublePinyinSchemas");
    }

    private void LoadDialogJumpOptions()
    {
        DialogJumpWindowPositions = DropdownDataGeneric<DialogJumpWindowPositions>.GetEnumData("DialogJumpWindowPosition");
        DialogJumpResultBehaviours = DropdownDataGeneric<DialogJumpResultBehaviours>.GetEnumData("DialogJumpResultBehaviour");
        DialogJumpFileResultBehaviours = DropdownDataGeneric<DialogJumpFileResultBehaviours>.GetEnumData("DialogJumpFileResultBehaviour");
    }

    private static int GetScreenCount()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Screens?.All is { Count: > 0 } screens)
        {
            return screens.Count;
        }

        return 1;
    }

    public sealed class HistoryStyleOption
    {
        public HistoryStyleOption(HistoryStyle value, string display)
        {
            Value = value;
            Display = display;
        }

        public HistoryStyle Value { get; }

        public string Display { get; private set; }

        public void UpdateLabel(AvaloniaI18n i18n)
        {
            Display = Value switch
            {
                HistoryStyle.Query => i18n.GetTranslation("queryHistory"),
                HistoryStyle.LastOpened => i18n.GetTranslation("executedHistory"),
                _ => Display
            };
        }
    }

    #endregion
}

