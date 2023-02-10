using System.Windows;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views
{
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

        public SettingsControl(Settings settings)
        {
            Settings = settings;
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
        private void Others_Click(object sender, RoutedEventArgs e)
        {

            if (CustomBrowsersList.Visibility == Visibility.Collapsed)
            {
                CustomBrowsersList.Visibility = Visibility.Visible;
            }
            else
                CustomBrowsersList.Visibility = Visibility.Collapsed;
        }

        private void EditCustomBrowser(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomBrowser is null)
                return;

            var window = new CustomBrowserSettingWindow(SelectedCustomBrowser);
            window.ShowDialog();
        }
    }
}
