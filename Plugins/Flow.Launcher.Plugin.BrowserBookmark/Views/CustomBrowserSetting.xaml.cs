using Flow.Launcher.Plugin.BrowserBookmark.Models;
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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views
{
    /// <summary>
    /// Interaction logic for CustomBrowserSetting.xaml
    /// </summary>
    public partial class CustomBrowserSettingWindow : Window
    {
        private CustomBrowser currentCustomBrowser;
        public CustomBrowserSettingWindow(CustomBrowser browser)
        {
            InitializeComponent();
            currentCustomBrowser = browser;
            DataContext = new CustomBrowser
            {
                Name = browser.Name,
                DataDirectoryPath = browser.DataDirectoryPath
            };
        }

        private void ConfirmEditCustomBrowser(object sender, RoutedEventArgs e)
        {
            if (DataContext is CustomBrowser editedBrowser)
            {
                currentCustomBrowser.Name = editedBrowser.Name;
                currentCustomBrowser.DataDirectoryPath = editedBrowser.DataDirectoryPath;
            }
            Close();
        }
    }
}
