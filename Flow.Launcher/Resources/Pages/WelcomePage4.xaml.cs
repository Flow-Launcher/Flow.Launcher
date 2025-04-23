using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage4
    {
        public Settings Settings { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!IsInitialized)
            {
                Settings = Ioc.Default.GetRequiredService<Settings>();
                InitializeComponent();
            }
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            Ioc.Default.GetRequiredService<WelcomeViewModel>().PageNum = 4;
            base.OnNavigatedTo(e);
        }
    }
}
