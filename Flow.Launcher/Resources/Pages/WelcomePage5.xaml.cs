using System;
using System.Windows;
using System.Windows.Navigation;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage5
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        public Settings Settings { get; set; }
        public bool HideOnStartup { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else
                throw new ArgumentException("Unexpected Navigation Parameter for Settings");
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

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window.Close();
        }

    }
}
