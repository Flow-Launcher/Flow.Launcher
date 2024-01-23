using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.ViewModels.WelcomePages
{
    public class WelcomePage2ViewModel : PageViewModelBase
    {
        public override bool CanNavigateNext { get; protected set; } = true;
        public override bool CanNavigatePrevious { get; protected set; } = true;
        public override string PageTitle { get; }

        public Settings Settings { get; set; }
        public WelcomePage2ViewModel(Settings settings)
        {
            Settings = settings;
        }
        
    }
}
