using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.ViewModels.WelcomePages
{
    public class WelcomePage4ViewModel : PageViewModelBase
    {
        public override bool CanNavigateNext { get; protected set; } = true;
        public override bool CanNavigatePrevious { get; protected set; } = true;
        public override string PageTitle { get; }
        
        public WelcomePage4ViewModel(Settings settings)
        {
            Settings = settings;
        }

        public Settings Settings { get; set; }
    }
}
