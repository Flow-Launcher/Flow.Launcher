using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;

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

        private void ConfirmCancelEditCustomBrowser(object sender, RoutedEventArgs e)
        {
            if (DataContext is CustomBrowser editBrowser && e.Source is System.Windows.Controls.Button button)
            {
                if (button.Name == "btnConfirm")
                {
                    currentCustomBrowser.Name = editBrowser.Name;
                    currentCustomBrowser.DataDirectoryPath = editBrowser.DataDirectoryPath;
                    currentCustomBrowser.BrowserType = editBrowser.BrowserType;
                    Close();
                }
            }

            Close();
        }

        private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmCancelEditCustomBrowser(sender, e);
            }
        }

        private void OnSelectPathClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            PathTextBox.Text = dialog.SelectedPath;
            string folder = dialog.SelectedPath;
        }
    }
}
