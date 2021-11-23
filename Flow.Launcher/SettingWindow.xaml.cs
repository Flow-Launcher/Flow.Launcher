using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using Microsoft.Win32;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Helper;
using System.Windows.Controls;
using Flow.Launcher.Core.ExternalPlugins;
using System.Runtime.InteropServices;
using ThemeManager = ModernWpf.ThemeManager;
using ApplicationTheme = ModernWpf.ApplicationTheme;

namespace Flow.Launcher
{
    public partial class SettingWindow
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public readonly IPublicAPI API;
        private Settings settings;
        private SettingWindowViewModel viewModel;
        private static MainViewModel mainViewModel;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
        {
            InitializeComponent();
            settings = viewModel.Settings;
            DataContext = viewModel;
            this.viewModel = viewModel;
            API = api;
        }

        #region General
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshMaximizeRestoreButton();
            // Fix (workaround) for the window freezes after lock screen (Win+L)
            // https://stackoverflow.com/questions/4951058/software-rendering-mode-wpf
            HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            HwndTarget hwndTarget = hwndSource.CompositionTarget;
            hwndTarget.RenderMode = RenderMode.SoftwareOnly;
        }

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

        private void OnSelectFileManagerClick(object sender, RoutedEventArgs e)
        {
                SelectFileManagerWindow fileManagerChangeWindow = new SelectFileManagerWindow(settings);
                fileManagerChangeWindow.ShowDialog();
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

                HotKeyMapper.SetHotkey(HotkeyControl.CurrentHotkey, HotKeyMapper.OnToggleHotkey);
                HotKeyMapper.RemoveHotkey(settings.Hotkey);
                settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
            }
        }

        private void OnDeleteCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
            var item = viewModel.SelectedCustomPluginHotkey;
            if (item == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
                return;
            }

            string deleteWarning =
                string.Format(InternationalizationManager.Instance.GetTranslation("deleteCustomHotkeyWarning"),
                    item.Hotkey);
            if (
                MessageBox.Show(deleteWarning, InternationalizationManager.Instance.GetTranslation("delete"),
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                settings.CustomPluginHotkeys.Remove(item);
                HotKeyMapper.RemoveHotkey(item.Hotkey);
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
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
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

        private void OnPluginPriorityClick(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: PluginViewModel pluginViewModel })
            {
                PriorityChangeWindow priorityChangeWindow = new PriorityChangeWindow(pluginViewModel.PluginPair.Metadata.ID, settings, pluginViewModel);
                priorityChangeWindow.ShowDialog();
            }
        }

        private void OnPluginActionKeywordsClick(object sender, RoutedEventArgs e)
        {
            var id = viewModel.SelectedPlugin.PluginPair.Metadata.ID;
            ActionKeywords changeKeywordsWindow = new ActionKeywords(id, settings, viewModel.SelectedPlugin);
            changeKeywordsWindow.ShowDialog();
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
                    PluginManager.API.OpenDirectory(directory);
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
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Themes));
        }

        private void OpenSettingFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Settings));
        }

        private void OpenLogFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Logs));
        }

        private void OnPluginStoreRefreshClick(object sender, RoutedEventArgs e)
        {
            _ = viewModel.RefreshExternalPluginsAsync();
        }

        private void OnExternalPluginInstallClick(object sender, RoutedEventArgs e)
        {
            if(sender is Button { DataContext: UserPlugin plugin })
            {
                var pluginsManagerPlugin = PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7");
                var actionKeywrod = pluginsManagerPlugin.Metadata.ActionKeywords.Count == 0 ? "" : pluginsManagerPlugin.Metadata.ActionKeywords[0];
                API.ChangeQuery($"{actionKeywrod} install {plugin.Name}");
                API.ShowMainWindow();
            }
        }

        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            TextBox textBox = Keyboard.FocusedElement as TextBox;
            if (textBox != null)
            {
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                textBox.MoveFocus(tRequest);
            }
        }

        private void DarkModeSelectedIndexChanged(object sender, EventArgs e)
        {
            if (settings.DarkMode == "Light")
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
            }
            else if (settings.DarkMode == "Dark")
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
            }

        }

        /* Custom TitleBar */

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshMaximizeRestoreButton()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.maximizeButton.Visibility = Visibility.Collapsed;
                this.restoreButton.Visibility = Visibility.Visible;
            }
            else
            {
                this.maximizeButton.Visibility = Visibility.Visible;
                this.restoreButton.Visibility = Visibility.Collapsed;
            }
        }
        private void Window_StateChanged(object sender, EventArgs e)
        {
            this.RefreshMaximizeRestoreButton();
        }

    }
}