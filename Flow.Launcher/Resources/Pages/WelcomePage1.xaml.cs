using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Interop;
using Microsoft.Win32;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Resources.Pages
{
    /// <summary>
    /// WelcomePage1.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WelcomePage1 : Page
    {

        public WelcomePage1(Settings settings)
        {
            Settings = settings;
            InitializeComponent();
        }
        private Internationalization _translater => InternationalizationManager.Instance;
        public List<Language> Languages => _translater.LoadAvailableLanguages();

        public Settings Settings { get; }

        public string CustomLanguage
        {
            get
            {
                return Settings.Language;
            }
            set
            {
                InternationalizationManager.Instance.ChangeLanguage(value);

                if (InternationalizationManager.Instance.PromptShouldUsePinyin(value))
                    Settings.ShouldUsePinyin = true;
            }
        }

    }
}
