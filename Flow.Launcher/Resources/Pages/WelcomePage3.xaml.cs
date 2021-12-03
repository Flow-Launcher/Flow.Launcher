using System;
using System.Windows.Navigation;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage3
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else if(Settings is null)
                throw new ArgumentException("Unexpected Navigation Parameter for Settings");
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
