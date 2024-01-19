using System.Collections.Generic;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.ViewModel.WelcomePages
{
    public class WelcomePage1ViewModel : PageViewModelBase
    {
        public override bool CanNavigateNext { get; protected set; } = true;
        public override bool CanNavigatePrevious { get; protected set; } = false;
        public override string PageTitle { get; }

        public WelcomePage1ViewModel(Settings settings)
        {
            PageTitle = _translater.GetTranslation("welcomePage1Title");
            Settings = settings;
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
