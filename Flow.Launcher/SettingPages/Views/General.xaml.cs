using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Navigation;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;

namespace Flow.Launcher.SettingPages.Views
{
    public partial class General
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!IsInitialized)
            {
                if(e.ExtraData is not Settings settings)
                    throw new ArgumentException("Settings is not passed to General page");
                DataContext = new GeneralViewModel(settings);
                InitializeComponent();
            }
            base.OnNavigatedTo(e);
        }

       
    }
}
