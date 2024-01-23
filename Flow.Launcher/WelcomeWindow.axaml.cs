using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flow.Launcher.Infrastructure.UserSettings;
using ModernWpf.Media.Animation;
using PropertyChanged;

namespace Flow.Launcher
{
    [DoNotNotify]
    public partial class WelcomeWindow : Window
    {
        private readonly Settings settings;

        public WelcomeWindow(Settings settings)
        {
            DataContext = new ViewModel.WelcomeWindowViewModel(settings);

            InitializeComponent();
            this.settings = settings;
        }
    }
}
