using Avalonia.Interactivity;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.ViewModel.WelcomePages
{
    public class WelcomePage2ViewModel : PageViewModelBase
    {
        public override bool CanNavigateNext { get; protected set; }
        public override bool CanNavigatePrevious { get; protected set; }
        public override string PageTitle { get; }

        public Settings Settings { get; set; }
        public WelcomePage2ViewModel(Settings settings)
        {
            Settings = settings;
        }
        
    }
}
