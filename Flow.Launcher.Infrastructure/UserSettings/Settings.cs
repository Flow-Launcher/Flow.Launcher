using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class Settings : BaseModel
    {
        private string language = "en";

        public string Hotkey { get; set; } = $"{KeyConstant.Alt} + {KeyConstant.Space}";
        public string OpenResultModifiers { get; set; } = KeyConstant.Alt;
        public bool ShowOpenResultHotkey { get; set; } = true;
        public string Language
        {
            get => language; set
            {
                language = value;
                OnPropertyChanged();
            }
        }
        public string Theme { get; set; } = Constant.DefaultTheme;
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


        /// <summary>
        /// when false Alphabet static service will always return empty results
        /// </summary>
        public bool ShouldUsePinyin { get; set; } = false;

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
        public int MaxResultsToShow { get; set; } = 5;
        public int ActivateTimes { get; set; }


        public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = new ObservableCollection<CustomPluginHotkey>();

        public bool DontPromptUpdateMsg { get; set; }
        public bool EnableUpdateLog { get; set; }

        public bool StartFlowLauncherOnSystemStartup { get; set; } = true;
        public bool HideOnStartup { get; set; }
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
        public bool HideWhenDeactive { get; set; } = true;
        public bool RememberLastLaunchLocation { get; set; }
        public bool IgnoreHotkeysOnFullscreen { get; set; }

        public bool AutoHideScrollBar { get; set; }

        public HttpProxy Proxy { get; set; } = new HttpProxy();

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LastQueryMode LastQueryMode { get; set; } = LastQueryMode.Selected;


        // This needs to be loaded last by staying at the bottom
        public PluginsSettings PluginSettings { get; set; } = new PluginsSettings();
    }

    public enum LastQueryMode
    {
        Selected,
        Empty,
        Preserved
    }
}