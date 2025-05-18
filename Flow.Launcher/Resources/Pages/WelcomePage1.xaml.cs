using System.Collections.Generic;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage1
    {
        public Settings Settings { get; } = Ioc.Default.GetRequiredService<Settings>();
        private readonly WelcomeViewModel _viewModel = Ioc.Default.GetRequiredService<WelcomeViewModel>();

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            _viewModel.PageNum = 1;

            if (!IsInitialized)
            {
                InitializeComponent();
            }
            base.OnNavigatedTo(e);
        }

        private readonly Internationalization _translater = Ioc.Default.GetRequiredService<Internationalization>();

        public List<Language> Languages => _translater.LoadAvailableLanguages();

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
