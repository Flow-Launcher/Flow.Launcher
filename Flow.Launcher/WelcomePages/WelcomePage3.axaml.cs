using System;
using System.Windows.Navigation;
using Avalonia.Controls;
using Flow.Launcher.Infrastructure.UserSettings;
using PropertyChanged;

namespace Flow.Launcher.WelcomePages
{
    [DoNotNotify]
    public partial class WelcomePage3 : UserControl
    {
        public WelcomePage3()
        {
            InitializeComponent();
        }

        public Settings Settings { get; set; }
    }
}
