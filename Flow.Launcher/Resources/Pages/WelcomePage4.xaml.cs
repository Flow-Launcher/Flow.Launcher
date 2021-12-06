using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Windows.Navigation;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage4
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else
                throw new ArgumentException("Unexpected Navigation Parameter for Settings");
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
