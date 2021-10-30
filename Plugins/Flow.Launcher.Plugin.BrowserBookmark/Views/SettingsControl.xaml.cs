using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Windows.Input;
using System.ComponentModel;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views
{
    /// <summary>
    /// Interaction logic for BrowserBookmark.xaml
    /// </summary>
    public partial class SettingsControl : INotifyPropertyChanged
    {
        public Settings Settings { get; }
        public CustomBrowser SelectedCustomBrowser { get; set; }
        public bool OpenInNewBrowserWindow
        {
            get => Settings.OpenInNewBrowserWindow;
            set
            {
                Settings.OpenInNewBrowserWindow = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenInNewBrowserWindow)));
            }
        }
        public bool OpenInNewTab
        {
            get => !OpenInNewBrowserWindow;
        }

        public SettingsControl(Settings settings)
        {
            Settings = settings;
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
                    DataDirectoryPath: null
                })
            {
                Settings.CustomChromiumBrowsers.Add(newBrowser);
            }
        }

        private void DeleteCustomBrowser(object sender, RoutedEventArgs e)
        {
            if (CustomBrowsers.SelectedItem is CustomBrowser selectedCustomBrowser)
            {
                Settings.CustomChromiumBrowsers.Remove(selectedCustomBrowser);
            }
        }
        private void MouseDoubleClickOnSelectedCustomBrowser(object sender, MouseButtonEventArgs e)
        {
            if (SelectedCustomBrowser is null)
                return;

            var window = new CustomBrowserSettingWindow(SelectedCustomBrowser);
            window.ShowDialog();
        }
    }
}
