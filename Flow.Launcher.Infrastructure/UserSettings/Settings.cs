using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class Settings : BaseModel, IHotkeySettings
    {
        private FlowLauncherJsonStorage<Settings> _storage;
        private StringMatcher _stringMatcher = null;

        public void SetStorage(FlowLauncherJsonStorage<Settings> storage)
        {
            _storage = storage;
        }

        public void Initialize()
        {
            // Initialize dependency injection instances after Ioc.Default is created
            _stringMatcher = Ioc.Default.GetRequiredService<StringMatcher>();

            // Initialize application resources after application is created
            var settingWindowFont = new FontFamily(SettingWindowFont);
            Application.Current.Resources["SettingWindowFont"] = settingWindowFont;
            Application.Current.Resources["ContentControlThemeFontFamily"] = settingWindowFont;
        }

        public void Save()
        {
            _storage.Save();
        }

        private string _openResultModifiers = KeyConstant.Alt;
        public string OpenResultModifiers
        {
            get => _openResultModifiers;
            set
            {
                if (_openResultModifiers != value)
                {
                    _openResultModifiers = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ColorScheme { get; set; } = "System";

        private bool _showOpenResultHotkey = true;
        public bool ShowOpenResultHotkey
        {
            get => _showOpenResultHotkey;
            set
            {
                if (_showOpenResultHotkey != value)
                {
                    _showOpenResultHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        public double WindowSize { get; set; } = 580;

        private string _hotkey = $"{KeyConstant.Alt} + {KeyConstant.Space}";
        public string Hotkey
        {
            get => _hotkey;
            set
            {
                if (_hotkey != value)
                {
                    _hotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _previewHotkey = "F1";
        public string PreviewHotkey
        {
            get => _previewHotkey;
            set
            {
                if (_previewHotkey != value)
                {
                    _previewHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _autoCompleteHotkey = $"{KeyConstant.Ctrl} + Tab";
        public string AutoCompleteHotkey
        {
            get => _autoCompleteHotkey;
            set
            {
                if (_autoCompleteHotkey != value)
                {
                    _autoCompleteHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _autoCompleteHotkey2 = "";
        public string AutoCompleteHotkey2
        {
            get => _autoCompleteHotkey2;
            set
            {
                if (_autoCompleteHotkey2 != value)
                {
                    _autoCompleteHotkey2 = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectNextItemHotkey = "Tab";
        public string SelectNextItemHotkey
        {
            get => _selectNextItemHotkey;
            set
            {
                if (_selectNextItemHotkey != value)
                {
                    _selectNextItemHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectNextItemHotkey2 = "";
        public string SelectNextItemHotkey2
        {
            get => _selectNextItemHotkey2;
            set
            {
                if (_selectNextItemHotkey2 != value)
                {
                    _selectNextItemHotkey2 = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectPrevItemHotkey = "Shift + Tab";
        public string SelectPrevItemHotkey
        {
            get => _selectPrevItemHotkey;
            set
            {
                if (_selectPrevItemHotkey != value)
                {
                    _selectPrevItemHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectPrevItemHotkey2 = "";
        public string SelectPrevItemHotkey2
        {
            get => _selectPrevItemHotkey2;
            set
            {
                if (_selectPrevItemHotkey2 != value)
                {
                    _selectPrevItemHotkey2 = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectNextPageHotkey = "PageUp";
        public string SelectNextPageHotkey
        {
            get => _selectNextPageHotkey;
            set
            {
                if (_selectNextPageHotkey != value)
                {
                    _selectNextPageHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectPrevPageHotkey = "PageDown";
        public string SelectPrevPageHotkey
        {
            get => _selectPrevPageHotkey;
            set
            {
                if (_selectPrevPageHotkey != value)
                {
                    _selectPrevPageHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _openContextMenuHotkey = "Ctrl+O";
        public string OpenContextMenuHotkey
        {
            get => _openContextMenuHotkey;
            set
            {
                if (_openContextMenuHotkey != value)
                {
                    _openContextMenuHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _settingWindowHotkey = "Ctrl+I";
        public string SettingWindowHotkey
        {
            get => _settingWindowHotkey;
            set
            {
                if (_settingWindowHotkey != value)
                {
                    _settingWindowHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _openHistoryHotkey = "Ctrl+H";
        public string OpenHistoryHotkey
        {
            get => _openHistoryHotkey;
            set
            {
                if (_openHistoryHotkey != value)
                {
                    _openHistoryHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _cycleHistoryUpHotkey = $"{KeyConstant.Alt} + Up";
        public string CycleHistoryUpHotkey
        {
            get => _cycleHistoryUpHotkey;
            set
            {
                if (_cycleHistoryUpHotkey != value)
                {
                    _cycleHistoryUpHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _cycleHistoryDownHotkey = $"{KeyConstant.Alt} + Down";
        public string CycleHistoryDownHotkey
        {
            get => _cycleHistoryDownHotkey;
            set
            {
                if (_cycleHistoryDownHotkey != value)
                {
                    _cycleHistoryDownHotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _language = Constant.SystemLanguageCode;
        public string Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _theme = Constant.DefaultTheme;
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme != value)
                {
                    _theme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MaxResultsToShow));
                }
            }
        }
        public bool UseDropShadowEffect { get; set; } = true;
        public BackdropTypes BackdropType { get; set; } = BackdropTypes.None;
        public string ReleaseNotesVersion { get; set; } = string.Empty;

        /* Appearance Settings. It should be separated from the setting later.*/
        public double WindowHeightSize { get; set; } = 42;
        public double ItemHeightSize { get; set; } = 58;
        public double QueryBoxFontSize { get; set; } = 16;
        public double ResultItemFontSize { get; set; } = 16;
        public double ResultSubItemFontSize { get; set; } = 13;
        public string QueryBoxFont { get; set; } = Win32Helper.GetSystemDefaultFont();
        public string QueryBoxFontStyle { get; set; }
        public string QueryBoxFontWeight { get; set; }
        public string QueryBoxFontStretch { get; set; }
        public string ResultFont { get; set; } = Win32Helper.GetSystemDefaultFont();
        public string ResultFontStyle { get; set; }
        public string ResultFontWeight { get; set; }
        public string ResultFontStretch { get; set; }
        public string ResultSubFont { get; set; } = Win32Helper.GetSystemDefaultFont();
        public string ResultSubFontStyle { get; set; }
        public string ResultSubFontWeight { get; set; }
        public string ResultSubFontStretch { get; set; }
        public bool UseGlyphIcons { get; set; } = true;
        public bool UseAnimation { get; set; } = true;
        public bool UseSound { get; set; } = true;
        public double SoundVolume { get; set; } = 50;
        public bool ShowBadges { get; set; } = false;
        public bool ShowBadgesGlobalOnly { get; set; } = false;

        private string _settingWindowFont { get; set; } = Win32Helper.GetSystemDefaultFont(false);
        public string SettingWindowFont
        {
            get => _settingWindowFont;
            set
            {
                if (_settingWindowFont != value)
                {
                    _settingWindowFont = value;
                    OnPropertyChanged();
                    if (Application.Current != null)
                    {
                        Application.Current.Resources["SettingWindowFont"] = new FontFamily(value);
                        Application.Current.Resources["ContentControlThemeFontFamily"] = new FontFamily(value);
                    }
                }
            }
        }

        public bool UseClock { get; set; } = true;
        public bool UseDate { get; set; } = false;
        public string TimeFormat { get; set; } = "hh:mm tt";
        public string DateFormat { get; set; } = "MM'/'dd ddd";
        public bool FirstLaunch { get; set; } = true;

        public double SettingWindowWidth { get; set; } = 1000;
        public double SettingWindowHeight { get; set; } = 700;
        public double? SettingWindowTop { get; set; } = null;
        public double? SettingWindowLeft { get; set; } = null;
        public WindowState SettingWindowState { get; set; } = WindowState.Normal;

        private bool _showPlaceholder { get; set; } = true;
        public bool ShowPlaceholder
        {
            get => _showPlaceholder;
            set
            {
                if (_showPlaceholder != value)
                {
                    _showPlaceholder = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _placeholderText { get; set; } = string.Empty;
        public string PlaceholderText
        {
            get => _placeholderText;
            set
            {
                if (_placeholderText != value)
                {
                    _placeholderText = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showHomePage { get; set; } = true;
        public bool ShowHomePage
        {
            get => _showHomePage;
            set
            {
                if (_showHomePage != value)
                {
                    _showHomePage = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showHistoryResultsForHomePage = false;
        public bool ShowHistoryResultsForHomePage
        {
            get => _showHistoryResultsForHomePage;
            set
            {
                if (_showHistoryResultsForHomePage != value)
                {
                    _showHistoryResultsForHomePage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MaxHistoryResultsToShowForHomePage { get; set; } = 5;

        public bool AutoRestartAfterChanging { get; set; } = false;
        public bool ShowUnknownSourceWarning { get; set; } = true;
        public bool AutoUpdatePlugins { get; set; } = true;

        public int CustomExplorerIndex { get; set; } = 0;

        [JsonIgnore]
        public CustomExplorerViewModel CustomExplorer
        {
            get => CustomExplorerList[CustomExplorerIndex < CustomExplorerList.Count ? CustomExplorerIndex : 0];
            set => CustomExplorerList[CustomExplorerIndex] = value;
        }

        public List<CustomExplorerViewModel> CustomExplorerList { get; set; } = new()
        {
            new()
            {
                Name = "Explorer",
                Path = "explorer",
                DirectoryArgument = "\"%d\"",
                FileArgument = "/select, \"%f\"",
                Editable = false
            },
            new()
            {
                Name = "Total Commander",
                Path = @"C:\Program Files\totalcmd\TOTALCMD64.exe",
                DirectoryArgument = "/O /A /S /T \"%d\"",
                FileArgument = "/O /A /S /T \"%f\""
            },
            new()
            {
                Name = "Directory Opus",
                Path = @"C:\Program Files\GPSoftware\Directory Opus\dopusrt.exe",
                DirectoryArgument = "/cmd Go \"%d\" NEW",
                FileArgument = "/cmd Go \"%f\" NEW"

            },
            new()
            {
                Name = "Files",
                Path = "Files-Stable",
                DirectoryArgument = "\"%d\"",
                FileArgument = "-select \"%f\""
            }
        };

        public int CustomBrowserIndex { get; set; } = 0;

        [JsonIgnore]
        public CustomBrowserViewModel CustomBrowser
        {
            get => CustomBrowserList[CustomBrowserIndex];
            set => CustomBrowserList[CustomBrowserIndex] = value;
        }

        public List<CustomBrowserViewModel> CustomBrowserList { get; set; } = new()
        {
            new()
            {
                Name = "Default",
                Path = "*",
                PrivateArg = "",
                EnablePrivate = false,
                Editable = false
            },
            new()
            {
                Name = "Google Chrome",
                Path = "chrome",
                PrivateArg = "-incognito",
                EnablePrivate = false,
                Editable = false
            },
            new()
            {
                Name = "Mozilla Firefox",
                Path = "firefox",
                PrivateArg = "-private",
                EnablePrivate = false,
                Editable = false
            },
            new()
            {
                Name = "MS Edge",
                Path = "msedge",
                PrivateArg = "-inPrivate",
                EnablePrivate = false,
                Editable = false
            }
        };

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LOGLEVEL LogLevel { get; set; } = LOGLEVEL.INFO;

        /// <summary>
        /// when false Alphabet static service will always return empty results
        /// </summary>
        private bool _useAlphabet = true;
        public bool ShouldUsePinyin
        {
            get => _useAlphabet;
            set
            {
                if (_useAlphabet != value)
                {
                    _useAlphabet = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useDoublePinyin = false;
        public bool UseDoublePinyin
        {
            get => _useDoublePinyin;
            set
            {
                if (_useDoublePinyin != value)
                {
                    _useDoublePinyin = value;
                    OnPropertyChanged();
                }
            }
        }

        private DoublePinyinSchemas _doublePinyinSchema = DoublePinyinSchemas.XiaoHe;

        [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter))]
        public DoublePinyinSchemas DoublePinyinSchema
        {
            get => _doublePinyinSchema;
            set
            {
                if (_doublePinyinSchema != value)
                {
                    _doublePinyinSchema = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AlwaysPreview { get; set; } = false;

        public bool AlwaysStartEn { get; set; } = false;

        private SearchPrecisionScore _querySearchPrecision = SearchPrecisionScore.Regular;
        [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchPrecisionScore QuerySearchPrecision
        {
            get => _querySearchPrecision;
            set
            {
                if (_querySearchPrecision != value)
                {
                    _querySearchPrecision = value;
                    if (_stringMatcher != null)
                        _stringMatcher.UserSettingSearchPrecision = value;
                }
            }
        }

        public bool AutoUpdates { get; set; } = false;

        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public double PreviousScreenWidth { get; set; }
        public double PreviousScreenHeight { get; set; }
        public double PreviousDpiX { get; set; }
        public double PreviousDpiY { get; set; }

        /// <summary>
        /// Custom left position on selected monitor
        /// </summary>
        public double CustomWindowLeft { get; set; } = 0;

        /// <summary>
        /// Custom top position on selected monitor
        /// </summary>
        public double CustomWindowTop { get; set; } = 0;

        /// <summary>
        /// Fixed window size
        /// </summary>
        private bool _keepMaxResults { get; set; } = false;
        public bool KeepMaxResults
        {
            get => _keepMaxResults;
            set
            {
                if (_keepMaxResults != value)
                {
                    _keepMaxResults = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MaxResultsToShow { get; set; } = 5;

        public int ActivateTimes { get; set; }

        public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = new();

        public ObservableCollection<CustomShortcutModel> CustomShortcuts { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<BaseBuiltinShortcutModel> BuiltinShortcuts { get; set; } = new()
        {
            new AsyncBuiltinShortcutModel("{clipboard}", "shortcut_clipboard_description", () => Win32Helper.StartSTATaskAsync(Clipboard.GetText)),
            new BuiltinShortcutModel("{active_explorer_path}", "shortcut_active_explorer_path", FileExplorerHelper.GetActiveExplorerPath)
        };

        public bool DontPromptUpdateMsg { get; set; }
        public bool EnableUpdateLog { get; set; }

        public bool StartFlowLauncherOnSystemStartup { get; set; } = false;
        public bool UseLogonTaskForStartup { get; set; } = false;
        public bool HideOnStartup { get; set; } = true;
        private bool _hideNotifyIcon;
        public bool HideNotifyIcon
        {
            get => _hideNotifyIcon;
            set
            {
                if (_hideNotifyIcon != value)
                {
                    _hideNotifyIcon = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool LeaveCmdOpen { get; set; }
        public bool HideWhenDeactivated { get; set; } = true;

        private bool _showAtTopmost = false;
        public bool ShowAtTopmost
        {
            get => _showAtTopmost;
            set
            {
                if (_showAtTopmost != value)
                {
                    _showAtTopmost = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SearchQueryResultsWithDelay { get; set; }
        public int SearchDelayTime { get; set; } = 150;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchWindowScreens SearchWindowScreen { get; set; } = SearchWindowScreens.Cursor;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchWindowAligns SearchWindowAlign { get; set; } = SearchWindowAligns.Center;

        public int CustomScreenNumber { get; set; } = 1;

        public bool IgnoreHotkeysOnFullscreen { get; set; }

        public HttpProxy Proxy { get; set; } = new HttpProxy();

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LastQueryMode LastQueryMode { get; set; } = LastQueryMode.Selected;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AnimationSpeeds AnimationSpeed { get; set; } = AnimationSpeeds.Medium;
        public int CustomAnimationLength { get; set; } = 360;

        [JsonIgnore]
        public bool WMPInstalled { get; set; } = true;

        // This needs to be loaded last by staying at the bottom
        public PluginsSettings PluginSettings { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<RegisteredHotkeyData> RegisteredHotkeys { get; } = new();
    }

    public enum LastQueryMode
    {
        Selected,
        Empty,
        Preserved,
        ActionKeywordPreserved,
        ActionKeywordSelected
    }

    public enum ColorSchemes
    {
        System,
        Light,
        Dark
    }

    public enum SearchWindowScreens
    {
        RememberLastLaunchLocation,
        Cursor,
        Focus,
        Primary,
        Custom
    }

    public enum SearchWindowAligns
    {
        Center,
        CenterTop,
        LeftTop,
        RightTop,
        Custom
    }

    public enum AnimationSpeeds
    {
        Slow,
        Medium,
        Fast,
        Custom
    }

    public enum BackdropTypes
    {
        None,
        Acrylic,
        Mica,
        MicaAlt
    }

    public enum DoublePinyinSchemas
    {
        XiaoHe,
        ZiRanMa,
        WeiRuan,
        ZhiNengABC,
        ZiGuangPinYin,
        PinYinJiaJia,
        XingKongJianDao,
        DaNiu,
        XiaoLang
    }
}
