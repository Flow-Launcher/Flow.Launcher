using System;
using System.Windows;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage5
    {
        public Settings Settings { get; private set; }
        private WelcomeViewModel _viewModel;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!IsInitialized)
            {
                Settings = Ioc.Default.GetRequiredService<Settings>();
                _viewModel = Ioc.Default.GetRequiredService<WelcomeViewModel>();
                InitializeComponent();
            }
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            _viewModel.PageNum = 5;
            base.OnNavigatedTo(e);
        }

        private void OnAutoStartupChecked(object sender, RoutedEventArgs e)
        {
            ChangeAutoStartup(true);
        }

        private void OnAutoStartupUncheck(object sender, RoutedEventArgs e)
        {
            ChangeAutoStartup(false);
        }

        private void ChangeAutoStartup(bool value)
        {
            Settings.StartFlowLauncherOnSystemStartup = value;
            try
            {
                if (value)
                {
                    if (Settings.UseLogonTaskForStartup)
                    {
                        AutoStartup.ChangeToViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                else
                {
                    AutoStartup.DisableViaLogonTaskAndRegistry();
                }
            }
            catch (Exception e)
            {
                App.API.ShowMsg(App.API.GetTranslation("setAutoStartFailed"), e.Message);
            }
        }

        private void OnUseLogonTaskChecked(object sender, RoutedEventArgs e)
        {
            ChangeUseLogonTask(true);
        }

        private void OnUseLogonTaskUncheck(object sender, RoutedEventArgs e)
        {
            ChangeUseLogonTask(false);
        }

        private void ChangeUseLogonTask(bool value)
        {
            Settings.UseLogonTaskForStartup = value;
            if (Settings.StartFlowLauncherOnSystemStartup)
            {
                try
                {
                    if (value)
                    {
                        AutoStartup.ChangeToViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                catch (Exception e)
                {
                    App.API.ShowMsg(App.API.GetTranslation("setAutoStartFailed"), e.Message);
                }
            }
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
