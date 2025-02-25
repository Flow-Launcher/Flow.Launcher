using System;
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
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else
                throw new ArgumentException("Unexpected Navigation Parameter for Settings");
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
