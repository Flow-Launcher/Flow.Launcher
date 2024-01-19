using Avalonia.Controls;
using Avalonia.Interactivity;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;
using PropertyChanged;

namespace Flow.Launcher.WelcomePages
{
    [DoNotNotify]
    public partial class WelcomePage5 : UserControl
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        public Settings Settings { get; set; }
        public bool HideOnStartup { get; set; }

        public WelcomePage5()
        {
            InitializeComponent();
        }

        private void OnAutoStartupChecked(object sender, RoutedEventArgs e)
        {
            SetStartup();
        }

        private void OnAutoStartupUncheck(object sender, RoutedEventArgs e)
        {
            RemoveStartup();
        }

        private void RemoveStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            key?.DeleteValue(Constant.FlowLauncher, false);
            Settings.StartFlowLauncherOnSystemStartup = false;
        }

        private void SetStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            key?.SetValue(Constant.FlowLauncher, Constant.ExecutablePath);
            Settings.StartFlowLauncherOnSystemStartup = true;
        }

        private void OnHideOnStartupChecked(object sender, RoutedEventArgs e)
        {
            Settings.HideOnStartup = true;
        }

        private void OnHideOnStartupUnchecked(object sender, RoutedEventArgs e)
        {
            Settings.HideOnStartup = false;
        }
    }
}
