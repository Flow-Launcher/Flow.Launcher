using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Win32;
using NHotkey;
using NHotkey.Wpf;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class SettingWindow
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public readonly IPublicAPI API;
        private Settings settings;
        private SettingWindowViewModel viewModel;
        private readonly II18N _translator;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel, II18N translator)
        {
            InitializeComponent();
            settings = viewModel.Settings;
            DataContext = viewModel;
            this.viewModel = viewModel;
            _translator = translator;
            API = api;
        }

        #region General

        private void OnAutoStartupChecked(object sender, RoutedEventArgs e)
        {
            SetStartup();
        }

        private void OnAutoStartupUncheck(object sender, RoutedEventArgs e)
        {
            RemoveStartup();
        }

        public static void SetStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                key?.SetValue(Infrastructure.Constant.FlowLauncher, Infrastructure.Constant.ExecutablePath);
            }
        }

        private void RemoveStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                key?.DeleteValue(Infrastructure.Constant.FlowLauncher, false);
            }
        }

        public static bool StartupSet()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                var path = key?.GetValue(Infrastructure.Constant.FlowLauncher) as string;
                if (path != null)
                {
                    return path == Infrastructure.Constant.ExecutablePath;
                }
                else
                {
                    return false;
                }
            }
        }

        private void OnSelectPythonDirectoryClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string pythonDirectory = dlg.SelectedPath;
                if (!string.IsNullOrEmpty(pythonDirectory))
                {
                    var pythonPath = Path.Combine(pythonDirectory, PluginsLoader.PythonExecutable);
                    if (File.Exists(pythonPath))
                    {
                        settings.PluginSettings.PythonDirectory = pythonDirectory;
                        MessageBox.Show("Remember to restart Flow Launcher use new Python path");
                    }
                    else
                    {
                        MessageBox.Show("Can't find python in given directory");
                    }
                }
            }
        }

        #endregion

        #region Hotkey

        private void OnHotkeyControlLoaded(object sender, RoutedEventArgs e)
        {
            HotkeyControl.SetHotkey(viewModel.Settings.Hotkey, false);
        }

        void OnHotkeyChanged(object sender, EventArgs e)
        {
            if (HotkeyControl.CurrentHotkeyAvailable)
            {
                SetHotkey(HotkeyControl.CurrentHotkey, (o, args) =>
                {
                    if (!Application.Current.MainWindow.IsVisible)
                    {
                        Application.Current.MainWindow.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Application.Current.MainWindow.Visibility = Visibility.Hidden;
                    }
                });
                RemoveHotkey(settings.Hotkey);
                settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
            }
        }

        void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg =
                    string.Format(_translator.GetTranslation("registerHotkeyFailed"), hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        private void OnDeleteCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
            var item = viewModel.SelectedCustomPluginHotkey;
            if (item == null)
            {
                MessageBox.Show(_translator.GetTranslation("pleaseSelectAnItem"));
                return;
            }

            string deleteWarning =
                string.Format(_translator.GetTranslation("deleteCustomHotkeyWarning"),
                    item.Hotkey);
            if (
                MessageBox.Show(deleteWarning, _translator.GetTranslation("delete"),
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                settings.CustomPluginHotkeys.Remove(item);
                RemoveHotkey(item.Hotkey);
            }
        }

        private void OnnEditCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
            var item = viewModel.SelectedCustomPluginHotkey;
            if (item != null)
            {
                CustomQueryHotkeySetting window = new CustomQueryHotkeySetting(this, settings);
                window.UpdateItem(item);
                window.ShowDialog();
            }
            else
            {
                MessageBox.Show(_translator.GetTranslation("pleaseSelectAnItem"));
            }
        }

        private void OnAddCustomeHotkeyClick(object sender, RoutedEventArgs e)
        {
            new CustomQueryHotkeySetting(this, settings).ShowDialog();
        }

        #endregion

        #region Plugin

        private void OnPluginToggled(object sender, RoutedEventArgs e)
        {
            var id = viewModel.SelectedPlugin.PluginPair.Metadata.ID;
            // used to sync the current status from the plugin manager into the setting to keep consistency after save
            settings.PluginSettings.Plugins[id].Disabled = viewModel.SelectedPlugin.PluginPair.Metadata.Disabled;
        }

        private void OnPluginPriorityClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                PriorityChangeWindow priorityChangeWindow = new PriorityChangeWindow(viewModel.SelectedPlugin.PluginPair.Metadata.ID, settings, viewModel.SelectedPlugin);
                priorityChangeWindow.ShowDialog();
            }
        }

        private void OnPluginActionKeywordsClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var id = viewModel.SelectedPlugin.PluginPair.Metadata.ID;
                ActionKeywords changeKeywordsWindow = new ActionKeywords(id, settings, viewModel.SelectedPlugin);
                changeKeywordsWindow.ShowDialog();
            }
        }

        private void OnPluginNameClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var website = viewModel.SelectedPlugin.PluginPair.Metadata.Website;
                if (!string.IsNullOrEmpty(website))
                {
                    var uri = new Uri(website);
                    if (Uri.CheckSchemeName(uri.Scheme))
                    {
                        SearchWeb.NewTabInBrowser(website);
                    }
                }
            }
        }

        private void OnPluginDirecotyClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var directory = viewModel.SelectedPlugin.PluginPair.Metadata.PluginDirectory;
                if (!string.IsNullOrEmpty(directory))
                    FilesFolders.OpenPath(directory);
            }
        }
        #endregion

        #region Proxy

        private void OnTestProxyClick(object sender, RoutedEventArgs e)
        { // TODO: change to command
            var msg = viewModel.TestProxy();
            MessageBox.Show(msg); // TODO: add message box service
        }

        #endregion

        private async void OnCheckUpdates(object sender, RoutedEventArgs e)
        {
            viewModel.UpdateApp(); // TODO: change to command
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            SearchWeb.NewTabInBrowser(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            viewModel.Save();
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void OpenPluginFolder(object sender, RoutedEventArgs e)
        {
            FilesFolders.OpenPath(Path.Combine(DataLocation.DataDirectory(), Constant.Themes));
        }

    }
}