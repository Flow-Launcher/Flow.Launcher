using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.Threading.Tasks;

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
                DataDirectoryPath = browser.DataDirectoryPath,
                BrowserType = browser.BrowserType,
            };
        }
        
        private void ConfirmEditCustomBrowser(object sender, RoutedEventArgs e)
        {
            CustomBrowser editBrowser = (CustomBrowser)DataContext;
            currentCustomBrowser.Name = editBrowser.Name;
            currentCustomBrowser.DataDirectoryPath = editBrowser.DataDirectoryPath;
            currentCustomBrowser.BrowserType = editBrowser.BrowserType;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
            Close();
        }

        private void CancelEditCustomBrowser(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmEditCustomBrowser(sender, e);
            }
        }

        private void OnSelectPathClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            CustomBrowser editBrowser = (CustomBrowser)DataContext;
            editBrowser.DataDirectoryPath = dialog.SelectedPath;
        }
    }
}
