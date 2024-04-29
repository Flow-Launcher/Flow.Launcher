using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Helper;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using static Flow.Launcher.SettingPages.ViewModels.GeneralViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Helper;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Infrastructure;
using static Flow.Launcher.ViewModel.SettingWindowViewModel;

namespace Flow.Launcher.SettingPages.ViewModels
{
    public partial class GeneralViewModel
    {
        public Settings Settings { get; set; }
        private readonly Updater _updater;
        private readonly IPortable _portable;

        public GeneralViewModel(Settings settings)
        {
            Settings = settings;
        }

        public class SearchWindowScreen
        {
            public string Display { get; set; }
            public SearchWindowScreens Value { get; set; }
        }


        public bool StartFlowLauncherOnSystemStartup
        {
            get => Settings.StartFlowLauncherOnSystemStartup;
            set
            {
                Settings.StartFlowLauncherOnSystemStartup = value;

                try
                {
                    if (value)
                        AutoStartup.Enable();
                    else
                        AutoStartup.Disable();
                }
                catch (Exception e)
                {
                    Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"),
                        e.Message);
                }
            }
        }


        private Internationalization _translater => InternationalizationManager.Instance;
        public List<SearchWindowScreen> SearchWindowScreens
        {
            get
            {
                List<SearchWindowScreen> modes = new List<SearchWindowScreen>();
                var enums = (SearchWindowScreens[])Enum.GetValues(typeof(SearchWindowScreens));
                foreach (var e in enums)
                {
                    var key = $"SearchWindowScreen{e}";
                    var display = _translater.GetTranslation(key);
                    var m = new SearchWindowScreen { Display = display, Value = e, };
                    modes.Add(m);
                }

                return modes;
            }


        }

        public List<SearchWindowAlign> SearchWindowAligns
        {
            get
            {
                List<SearchWindowAlign> modes = new List<SearchWindowAlign>();
                var enums = (SearchWindowAligns[])Enum.GetValues(typeof(SearchWindowAligns));
                foreach (var e in enums)
                {
                    var key = $"SearchWindowAlign{e}";
                    var display = _translater.GetTranslation(key);
                    var m = new SearchWindowAlign { Display = display, Value = e, };
                    modes.Add(m);
                }

                return modes;
            }
        }

        public List<int> ScreenNumbers
        {
            get
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var screenNumbers = new List<int>();
                for (int i = 1; i <= screens.Length; i++)
                {
                    screenNumbers.Add(i);
                }

                return screenNumbers;
            }
        }

        // This is only required to set at startup. When portable mode enabled/disabled a restart is always required
        private bool _portableMode = DataLocation.PortableDataLocationInUse();

        public bool PortableMode
        {
            get => _portableMode;
            set
            {
                if (!_portable.CanUpdatePortability())
                    return;

                if (DataLocation.PortableDataLocationInUse())
                {
                    _portable.DisablePortableMode();
                }
                else
                {
                    _portable.EnablePortableMode();
                }
            }
        }


        // todo a better name?
        public class LastQueryMode : BaseModel
        {
            public string Display { get; set; }
            public Infrastructure.UserSettings.LastQueryMode Value { get; set; }
        }

        private List<LastQueryMode> _lastQueryModes = new List<LastQueryMode>();

        public List<LastQueryMode> LastQueryModes
        {
            get
            {
                if (_lastQueryModes.Count == 0)
                {
                    _lastQueryModes = InitLastQueryModes();
                }

                return _lastQueryModes;
            }
        }

        private List<LastQueryMode> InitLastQueryModes()
        {
            var modes = new List<LastQueryMode>();
            var enums = (Infrastructure.UserSettings.LastQueryMode[])Enum.GetValues(
                typeof(Infrastructure.UserSettings.LastQueryMode));
            foreach (var e in enums)
            {
                var key = $"LastQuery{e}";
                var display = _translater.GetTranslation(key);
                var m = new LastQueryMode { Display = display, Value = e, };
                modes.Add(m);
            }

            return modes;
        }

        private void UpdateLastQueryModeDisplay()
        {
            foreach (var item in LastQueryModes)
            {
                item.Display = _translater.GetTranslation($"LastQuery{item.Value}");
            }
        }

        public string Language
        {
            get
            {
                return Settings.Language;
            }
            set
            {
                InternationalizationManager.Instance.ChangeLanguage(value);

                if (InternationalizationManager.Instance.PromptShouldUsePinyin(value))
                    ShouldUsePinyin = true;

                UpdateLastQueryModeDisplay();
            }
        }

        public bool ShouldUsePinyin
        {
            get
            {
                return Settings.ShouldUsePinyin;
            }
            set
            {
                Settings.ShouldUsePinyin = value;
            }
        }

        public List<string> QuerySearchPrecisionStrings
        {
            get
            {
                var precisionStrings = new List<string>();

                var enumList = Enum.GetValues(typeof(SearchPrecisionScore)).Cast<SearchPrecisionScore>().ToList();

                enumList.ForEach(x => precisionStrings.Add(x.ToString()));

                return precisionStrings;
            }
        }

        public List<string> OpenResultModifiersList => new List<string>
        {
            KeyConstant.Alt, KeyConstant.Ctrl, $"{KeyConstant.Ctrl}+{KeyConstant.Alt}"
        };

        public List<Language> Languages => _translater.LoadAvailableLanguages();
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

        public string AlwaysPreviewToolTip =>
            string.Format(_translater.GetTranslation("AlwaysPreviewToolTip"), Settings.PreviewHotkey);
    }
}
