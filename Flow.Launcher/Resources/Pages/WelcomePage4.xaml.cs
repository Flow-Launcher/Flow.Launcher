using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Navigation;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage4
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Settings = Ioc.Default.GetRequiredService<Settings>();
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
