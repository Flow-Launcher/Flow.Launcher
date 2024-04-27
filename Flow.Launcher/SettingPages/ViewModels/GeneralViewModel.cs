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
    }
}
