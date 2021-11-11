﻿using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;


namespace Flow.Launcher.Plugin.Url
{
    /// <summary>
    /// SettingsControl.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private Settings _settings;
        private IPublicAPI _flowlauncherAPI;

        public SettingsControl(IPublicAPI flowlauncherAPI,Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            _flowlauncherAPI = flowlauncherAPI;
            browserPathBox.Text = _settings.BrowserPath;
            NewWindowBrowser.IsChecked = _settings.OpenInNewBrowserWindow;
            NewTabInBrowser.IsChecked = !_settings.OpenInNewBrowserWindow;
        }

        private void OnChooseClick(object sender, RoutedEventArgs e)
        {
            var fileBrowserDialog = new OpenFileDialog();
            fileBrowserDialog.Filter = _flowlauncherAPI.GetTranslation("flowlauncher_plugin_url_plugin_filter"); ;
            fileBrowserDialog.CheckFileExists = true;
            fileBrowserDialog.CheckPathExists = true;
            if (fileBrowserDialog.ShowDialog() == true)
            {
                browserPathBox.Text = fileBrowserDialog.FileName;
                _settings.BrowserPath = fileBrowserDialog.FileName;
            }
        }

        private void OnNewBrowserWindowClick(object sender, RoutedEventArgs e)
        {
            _settings.OpenInNewBrowserWindow = true;
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            _settings.OpenInNewBrowserWindow = false;
        }

        private void OnBrowserPathTextChanged(object sender, TextChangedEventArgs e)
        {
            _settings.BrowserPath = browserPathBox.Text;
        }
    }
}
