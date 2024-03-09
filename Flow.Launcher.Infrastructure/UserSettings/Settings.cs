using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class Settings : BaseModel
    {
        private string language = "en";
        private string _theme = Constant.DefaultTheme;
        public string Hotkey { get; set; } = $"{KeyConstant.Alt} + {KeyConstant.Space}";
        public string OpenResultModifiers { get; set; } = KeyConstant.Alt;
        public string ColorScheme { get; set; } = "System";
        public bool ShowOpenResultHotkey { get; set; } = true;
        public double WindowSize { get; set; } = 580;
        public string PreviewHotkey { get; set; } = $"F1";

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
        public bool UseDropShadowEffect { get; set; } = false;
        public string QueryBoxFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string QueryBoxFontStyle { get; set; }
        public string QueryBoxFontWeight { get; set; }
        public string QueryBoxFontStretch { get; set; }
        public string ResultFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string ResultFontStyle { get; set; }
        public string ResultFontWeight { get; set; }
        public string ResultFontStretch { get; set; }
        public bool UseGlyphIcons { get; set; } = true;
        public bool UseAnimation { get; set; } = true;
        public bool UseSound { get; set; } = true;
        public bool UseClock { get; set; } = true;
        public bool UseDate { get; set; } = false;
        public string TimeFormat { get; set; } = "hh:mm tt";
        public string DateFormat { get; set; } = "MM'/'dd ddd";
        public bool FirstLaunch { get; set; } = true;

        public double SettingWindowWidth { get; set; } = 1000;
        public double SettingWindowHeight { get; set; } = 700;
        public double SettingWindowTop { get; set; }
        public double SettingWindowLeft { get; set; }
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

        public bool ShouldUseDoublePin { get; set; } = false;
        public bool AlwaysPreview { get; set; } = false;
        public bool AlwaysStartEn { get; set; } = false;

        [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchPrecisionScore QuerySearchPrecision { get; private set; } = SearchPrecisionScore.Regular;

        [JsonIgnore]
        public string QuerySearchPrecisionString
        {
            get { return QuerySearchPrecision.ToString(); }
            set
            {
                try
                {
                    var precisionScore = (SearchPrecisionScore)Enum
                        .Parse(typeof(SearchPrecisionScore), value);

                    QuerySearchPrecision = precisionScore;
                    StringMatcher.Instance.UserSettingSearchPrecision = precisionScore;
                }
                catch (ArgumentException e)
                {
                    Logger.Log.Exception(nameof(Settings), "Failed to load QuerySearchPrecisionString value from Settings file", e);

                    QuerySearchPrecision = SearchPrecisionScore.Regular;
                    StringMatcher.Instance.UserSettingSearchPrecision = SearchPrecisionScore.Regular;

                    throw;
                }
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


        // This needs to be loaded last by staying at the bottom
        public PluginsSettings PluginSettings { get; set; } = new PluginsSettings();
    }

    public enum LastQueryMode
    {
        Selected,
        Empty,
        Preserved
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
