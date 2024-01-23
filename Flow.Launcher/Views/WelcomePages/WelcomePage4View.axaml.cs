using Avalonia.Controls;
using Flow.Launcher.Infrastructure.UserSettings;
using PropertyChanged;

namespace Flow.Launcher.Views.WelcomePages
{
    [DoNotNotify]
    public partial class WelcomePage4View : UserControl
    {
        public WelcomePage4View()
        {
            InitializeComponent();
        }


        public Settings Settings { get; set; }
    }
}
