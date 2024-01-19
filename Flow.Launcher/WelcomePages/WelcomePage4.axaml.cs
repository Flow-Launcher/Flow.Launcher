using System;
using System.Windows.Navigation;
using Avalonia.Controls;
using Flow.Launcher.Infrastructure.UserSettings;
using PropertyChanged;

namespace Flow.Launcher.WelcomePages
{
    [DoNotNotify]
    public partial class WelcomePage4 : UserControl
    {
        public WelcomePage4()
        {
            InitializeComponent();
        }


        public Settings Settings { get; set; }
    }
}
