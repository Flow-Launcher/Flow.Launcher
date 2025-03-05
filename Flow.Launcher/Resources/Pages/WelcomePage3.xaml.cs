using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage3
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Settings = Ioc.Default.GetRequiredService<Settings>();
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
