using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views
{
    /// <summary>
    /// Interaction logic for BrowserBookmark.xaml
    /// </summary>
    public partial class SettingsControl
    {
        public Settings Settings { get; }

        public SettingsControl(Settings settings)
        {
            Settings = settings;
            InitializeComponent();
            NewWindowBrowser.IsChecked = Settings.OpenInNewBrowserWindow;
            NewTabInBrowser.IsChecked = !Settings.OpenInNewBrowserWindow;
        }

        private void OnNewBrowserWindowClick(object sender, RoutedEventArgs e)
        {
            Settings.OpenInNewBrowserWindow = true;
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            Settings.OpenInNewBrowserWindow = false;
        }

        private void OnChooseClick(object sender, RoutedEventArgs e)
        {
            var fileBrowserDialog = new OpenFileDialog();
            fileBrowserDialog.Filter = "Application(*.exe)|*.exe|All files|*.*";
            fileBrowserDialog.CheckFileExists = true;
            fileBrowserDialog.CheckPathExists = true;
            if (fileBrowserDialog.ShowDialog() == true)
            {
                Settings.BrowserPath = fileBrowserDialog.FileName;
            }
        }

        private void NewCustomBrowser(object sender, RoutedEventArgs e)
        {
            var newBrowser = new CustomBrowser();
            var window = new CustomBrowserSettingWindow(newBrowser);
            window.ShowDialog();
            if (newBrowser is not
                {
                    Name: null,
                    Path: null
                })
            {
                Settings.CustomBrowsers.Add(newBrowser);
            }
        }

        private void DeleteCustomBrowser(object sender, RoutedEventArgs e)
        {
            if(CustomBrowsers.SelectedItem is CustomBrowser selectedCustomBrowser)
            {
                Settings.CustomBrowsers.Remove(selectedCustomBrowser);
            }
        }
    }
}
