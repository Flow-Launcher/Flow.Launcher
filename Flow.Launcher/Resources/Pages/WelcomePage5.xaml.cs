using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Interop;
using Microsoft.Win32;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Resources.Pages
{
    /// <summary>
    /// WelcomePage5.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WelcomePage5 : Page
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
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
        }
        public static void SetStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            key?.SetValue(Constant.FlowLauncher, Constant.ExecutablePath);
        }


    }
}
