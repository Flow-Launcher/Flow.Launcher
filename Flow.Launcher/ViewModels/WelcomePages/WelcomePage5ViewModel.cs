using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;

namespace Flow.Launcher.ViewModels.WelcomePages
{
    public class WelcomePage5ViewModel : PageViewModelBase
    {
        public override bool CanNavigateNext { get; protected set; } = false;
        public override bool CanNavigatePrevious { get; protected set; } = true;
        public override string PageTitle { get; }

        public WelcomePage5ViewModel(Settings settings)
        {
            Settings = settings;
        }

        public bool AutoStartup
        {
            get
            {
                return Settings.StartFlowLauncherOnSystemStartup;
            }
            set
            {
                if (value)
                {
                    SetStartup();
                }
                else
                {
                    RemoveStartup();
                }

                Settings.StartFlowLauncherOnSystemStartup = value;
            }
        }

        public bool HideOnStartup
        {
            get
            {
                return Settings.HideOnStartup;
            }
            set
            {
                Settings.HideOnStartup = value;
            }
        }

        public Settings Settings { get; set; }

        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

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
    }
}
