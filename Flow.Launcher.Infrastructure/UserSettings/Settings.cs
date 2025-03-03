using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Hotkey;
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
            _stringMatcher = Ioc.Default.GetRequiredService<StringMatcher>();
        }

        public void Save()
        {
            _storage.Save();
        }

        private string language = Constant.SystemLanguageCode;
        private string _theme = Constant.DefaultTheme;
        public string Hotkey { get; set; } = $"{KeyConstant.Alt} + {KeyConstant.Space}";
        public string OpenResultModifiers { get; set; } = KeyConstant.Alt;
        public string ColorScheme { get; set; } = "System";
        public bool ShowOpenResultHotkey { get; set; } = true;
        public double WindowSize { get; set; } = 580;
        public string PreviewHotkey { get; set; } = $"F1";
        public string AutoCompleteHotkey { get; set; } = $"{KeyConstant.Ctrl} + Tab";
        public string AutoCompleteHotkey2 { get; set; } = $"";
        public string SelectNextItemHotkey { get; set; } = $"Tab";
        public string SelectNextItemHotkey2 { get; set; } = $"";
        public string SelectPrevItemHotkey { get; set; } = $"Shift + Tab";
        public string SelectPrevItemHotkey2 { get; set; } = $"";
        public string SelectNextPageHotkey { get; set; } = $"PageUp";
        public string SelectPrevPageHotkey { get; set; } = $"PageDown";
        public string OpenContextMenuHotkey { get; set; } = $"Ctrl+O";
        public string SettingWindowHotkey { get; set; } = $"Ctrl+I";
        public string CycleHistoryUpHotkey { get; set; } = $"{KeyConstant.Alt} + Up";
        public string CycleHistoryDownHotkey { get; set; } = $"{KeyConstant.Alt} + Down";

        public string Language
        {
            get => language;
            set
            {
                language = value;
                OnPropertyChanged();
            }
        }
        public string Theme
        {
            get => _theme;
            set
            {
                if (value == _theme)
                    return;
                _theme = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MaxResultsToShow));
            }
        }
        public bool UseDropShadowEffect { get; set; } = true;

        /* Appearance Settings. It should be separated from the setting later.*/
        public double WindowHeightSize { get; set; } = 42;
        public double ItemHeightSize { get; set; } = 58;
        public double QueryBoxFontSize { get; set; } = 20;
        public double ResultItemFontSize { get; set; } = 16;
        public double ResultSubItemFontSize { get; set; } = 13;
        public string QueryBoxFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string QueryBoxFontStyle { get; set; }
        public string QueryBoxFontWeight { get; set; }
        public string QueryBoxFontStretch { get; set; }
        public string ResultFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string ResultFontStyle { get; set; }
        public string ResultFontWeight { get; set; }
        public string ResultFontStretch { get; set; }
        public string ResultSubFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string ResultSubFontStyle { get; set; }
        public string ResultSubFontWeight { get; set; }
        public string ResultSubFontStretch { get; set; }
        public bool UseGlyphIcons { get; set; } = true;
        public bool UseAnimation { get; set; } = true;
        public bool UseSound { get; set; } = true;
        public double SoundVolume { get; set; } = 50;

        public bool UseClock { get; set; } = true;
        public bool UseDate { get; set; } = false;
        public string TimeFormat { get; set; } = "hh:mm tt";
        public string DateFormat { get; set; } = "MM'/'dd ddd";
        public bool FirstLaunch { get; set; } = true;

        public double SettingWindowWidth { get; set; } = 1000;
        public double SettingWindowHeight { get; set; } = 700;
        public double? SettingWindowTop { get; set; } = null;
        public double? SettingWindowLeft { get; set; } = null;
        public System.Windows.WindowState SettingWindowState { get; set; } = WindowState.Normal;

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
                Path = "Files",
                DirectoryArgument = "-select \"%d\"",
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

        /// <summary>
        /// when false Alphabet static service will always return empty results
        /// </summary>
        public bool ShouldUsePinyin { get; set; } = false;

        public bool AlwaysPreview { get; set; } = false;

        public bool AlwaysStartEn { get; set; } = false;

        private SearchPrecisionScore _querySearchPrecision = SearchPrecisionScore.Regular;
        [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchPrecisionScore QuerySearchPrecision
        {
            get => _querySearchPrecision;
            set
            {
                _querySearchPrecision = value;
                if (_stringMatcher != null)
                    _stringMatcher.UserSettingSearchPrecision = value;
            }
        }

        public bool AutoUpdates { get; set; } = false;

        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }

        /// <summary>
        /// Custom left position on selected monitor
        /// </summary>
        public double CustomWindowLeft { get; set; } = 0;

        /// <summary>
        /// Custom top position on selected monitor
        /// </summary>
        public double CustomWindowTop { get; set; } = 0;

        public bool KeepMaxResults { get; set; } = false;
        public int MaxResultsToShow { get; set; } = 5;
        public int ActivateTimes { get; set; }


        public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = new ObservableCollection<CustomPluginHotkey>();

        public ObservableCollection<CustomShortcutModel> CustomShortcuts { get; set; } = new ObservableCollection<CustomShortcutModel>();

        [JsonIgnore]
        public ObservableCollection<BuiltinShortcutModel> BuiltinShortcuts { get; set; } = new()
        {
            new BuiltinShortcutModel("{clipboard}", "shortcut_clipboard_description", Clipboard.GetText),
            new BuiltinShortcutModel("{active_explorer_path}", "shortcut_active_explorer_path", FileExplorerHelper.GetActiveExplorerPath)
        };

        public bool DontPromptUpdateMsg { get; set; }
        public bool EnableUpdateLog { get; set; }

        public bool StartFlowLauncherOnSystemStartup { get; set; } = false;
        public bool UseLogonTaskForStartup { get; set; } = false;
        public bool HideOnStartup { get; set; } = true;
        bool _hideNotifyIcon { get; set; }
        public bool HideNotifyIcon
        {
            get { return _hideNotifyIcon; }
            set
            {
                _hideNotifyIcon = value;
                OnPropertyChanged();
            }
        }
        public bool LeaveCmdOpen { get; set; }
        public bool HideWhenDeactivated { get; set; } = true;

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
        public PluginsSettings PluginSettings { get; set; } = new PluginsSettings();

        [JsonIgnore]
        public List<RegisteredHotkeyData> RegisteredHotkeys
        {
            get
            {
                var list = FixedHotkeys();

                // Customizeable hotkeys
                if(!string.IsNullOrEmpty(Hotkey))
                    list.Add(new(Hotkey, "flowlauncherHotkey", () => Hotkey = ""));
                if(!string.IsNullOrEmpty(PreviewHotkey))
                    list.Add(new(PreviewHotkey, "previewHotkey", () => PreviewHotkey = ""));
                if(!string.IsNullOrEmpty(AutoCompleteHotkey))
                    list.Add(new(AutoCompleteHotkey, "autoCompleteHotkey", () => AutoCompleteHotkey = ""));
                if(!string.IsNullOrEmpty(AutoCompleteHotkey2))
                    list.Add(new(AutoCompleteHotkey2, "autoCompleteHotkey", () => AutoCompleteHotkey2 = ""));
                if(!string.IsNullOrEmpty(SelectNextItemHotkey))
                    list.Add(new(SelectNextItemHotkey, "SelectNextItemHotkey", () => SelectNextItemHotkey = ""));
                if(!string.IsNullOrEmpty(SelectNextItemHotkey2))
                    list.Add(new(SelectNextItemHotkey2, "SelectNextItemHotkey", () => SelectNextItemHotkey2 = ""));
                if(!string.IsNullOrEmpty(SelectPrevItemHotkey))
                    list.Add(new(SelectPrevItemHotkey, "SelectPrevItemHotkey", () => SelectPrevItemHotkey = ""));
                if(!string.IsNullOrEmpty(SelectPrevItemHotkey2))
                    list.Add(new(SelectPrevItemHotkey2, "SelectPrevItemHotkey", () => SelectPrevItemHotkey2 = ""));
                if(!string.IsNullOrEmpty(SettingWindowHotkey))
                    list.Add(new(SettingWindowHotkey, "SettingWindowHotkey", () => SettingWindowHotkey = ""));
                if(!string.IsNullOrEmpty(OpenContextMenuHotkey))
                    list.Add(new(OpenContextMenuHotkey, "OpenContextMenuHotkey", () => OpenContextMenuHotkey = ""));
                if(!string.IsNullOrEmpty(SelectNextPageHotkey))
                    list.Add(new(SelectNextPageHotkey, "SelectNextPageHotkey", () => SelectNextPageHotkey = ""));
                if(!string.IsNullOrEmpty(SelectPrevPageHotkey))
                    list.Add(new(SelectPrevPageHotkey, "SelectPrevPageHotkey", () => SelectPrevPageHotkey = ""));
                if (!string.IsNullOrEmpty(CycleHistoryUpHotkey))
                    list.Add(new(CycleHistoryUpHotkey, "CycleHistoryUpHotkey", () => CycleHistoryUpHotkey = ""));
                if (!string.IsNullOrEmpty(CycleHistoryDownHotkey))
                    list.Add(new(CycleHistoryDownHotkey, "CycleHistoryDownHotkey", () => CycleHistoryDownHotkey = ""));

                // Custom Query Hotkeys
                foreach (var customPluginHotkey in CustomPluginHotkeys)
                {
                    if (!string.IsNullOrEmpty(customPluginHotkey.Hotkey))
                        list.Add(new(customPluginHotkey.Hotkey, "customQueryHotkey", () => customPluginHotkey.Hotkey = ""));
                }

                return list;
            }
        }

        private List<RegisteredHotkeyData> FixedHotkeys()
        {
            return new List<RegisteredHotkeyData>
            {
                new("Up", "HotkeyLeftRightDesc"),
                new("Down", "HotkeyLeftRightDesc"),
                new("Left", "HotkeyUpDownDesc"),
                new("Right", "HotkeyUpDownDesc"),
                new("Escape", "HotkeyESCDesc"),
                new("F5", "ReloadPluginHotkey"),
                new("Alt+Home", "HotkeySelectFirstResult"),
                new("Alt+End", "HotkeySelectLastResult"),
                new("Ctrl+R", "HotkeyRequery"),
                new("Ctrl+H", "ToggleHistoryHotkey"),
                new("Ctrl+OemCloseBrackets", "QuickWidthHotkey"),
                new("Ctrl+OemOpenBrackets", "QuickWidthHotkey"),
                new("Ctrl+OemPlus", "QuickHeightHotkey"),
                new("Ctrl+OemMinus", "QuickHeightHotkey"),
                new("Ctrl+Shift+Enter", "HotkeyCtrlShiftEnterDesc"),
                new("Shift+Enter", "OpenContextMenuHotkey"),
                new("Enter", "HotkeyRunDesc"),
                new("Ctrl+Enter", "OpenContainFolderHotkey"),
                new("Alt+Enter", "HotkeyOpenResult"),
                new("Ctrl+F12", "ToggleGameModeHotkey"),
                new("Ctrl+Shift+C", "CopyFilePathHotkey"),

                new($"{OpenResultModifiers}+D1", "HotkeyOpenResultN", 1),
                new($"{OpenResultModifiers}+D2", "HotkeyOpenResultN", 2),
                new($"{OpenResultModifiers}+D3", "HotkeyOpenResultN", 3),
                new($"{OpenResultModifiers}+D4", "HotkeyOpenResultN", 4),
                new($"{OpenResultModifiers}+D5", "HotkeyOpenResultN", 5),
                new($"{OpenResultModifiers}+D6", "HotkeyOpenResultN", 6),
                new($"{OpenResultModifiers}+D7", "HotkeyOpenResultN", 7),
                new($"{OpenResultModifiers}+D8", "HotkeyOpenResultN", 8),
                new($"{OpenResultModifiers}+D9", "HotkeyOpenResultN", 9),
                new($"{OpenResultModifiers}+D0", "HotkeyOpenResultN", 10)
            };
        }
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
}
