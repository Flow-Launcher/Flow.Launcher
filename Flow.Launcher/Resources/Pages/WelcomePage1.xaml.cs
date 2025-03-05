using System.Collections.Generic;
using System.Windows.Navigation;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Core.Resource;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage1
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Settings = Ioc.Default.GetRequiredService<Settings>();
            InitializeComponent();
        }
        private Internationalization _translater => InternationalizationManager.Instance;
        public List<Language> Languages => _translater.LoadAvailableLanguages();

        public Settings Settings { get; set; }

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
