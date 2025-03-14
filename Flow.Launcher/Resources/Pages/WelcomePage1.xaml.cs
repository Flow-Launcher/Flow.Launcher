using System.Collections.Generic;
using System.Windows.Navigation;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Core.Resource;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage1
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Settings = Ioc.Default.GetRequiredService<Settings>();
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            Ioc.Default.GetRequiredService<WelcomeViewModel>().PageNum = 1;
            InitializeComponent();
        }

        private readonly Internationalization _translater = Ioc.Default.GetRequiredService<Internationalization>();
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
                _translater.ChangeLanguage(value);

                if (_translater.PromptShouldUsePinyin(value))
                    Settings.ShouldUsePinyin = true;
            }
        }
    }
}
