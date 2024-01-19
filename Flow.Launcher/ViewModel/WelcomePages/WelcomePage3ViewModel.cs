using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.ViewModel.WelcomePages
{
    public class WelcomePage3ViewModel : PageViewModelBase
    {
        public override bool CanNavigateNext { get; protected set; }
        public override bool CanNavigatePrevious { get; protected set; }
        public override string PageTitle { get; }
        
        public WelcomePage3ViewModel(Settings settings)
        {
            Settings = settings;
        }
        
        public Settings Settings { get; set; }
    }
}
