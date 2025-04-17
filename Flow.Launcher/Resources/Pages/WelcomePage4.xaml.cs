using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using System.Windows.Navigation;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage4
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Settings = Ioc.Default.GetRequiredService<Settings>();
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            Ioc.Default.GetRequiredService<WelcomeViewModel>().PageNum = 5;
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
