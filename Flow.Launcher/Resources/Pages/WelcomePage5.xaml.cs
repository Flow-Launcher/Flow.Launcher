using System.Windows;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using Microsoft.Win32;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage5
    {
        public Settings Settings { get; private set; }

        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        public bool HideOnStartup { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!IsInitialized)
            {
                Settings = Ioc.Default.GetRequiredService<Settings>();
                InitializeComponent();
            }
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            Ioc.Default.GetRequiredService<WelcomeViewModel>().PageNum = 5;
            base.OnNavigatedTo(e);
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
