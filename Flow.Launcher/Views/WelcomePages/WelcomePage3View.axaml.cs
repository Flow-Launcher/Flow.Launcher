using Avalonia.Controls;
using Flow.Launcher.Infrastructure.UserSettings;
using PropertyChanged;

namespace Flow.Launcher.Views.WelcomePages
{
    [DoNotNotify]
    public partial class WelcomePage3View : UserControl
    {
        public WelcomePage3View()
        {
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
