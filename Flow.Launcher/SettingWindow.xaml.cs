﻿using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;
using ModernWpf;
using ModernWpf.Controls;
using System;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using ThemeManager = ModernWpf.ThemeManager;

namespace Flow.Launcher
{
    public partial class SettingWindow
    {
        public readonly IPublicAPI API;
        private Settings settings;
        private SettingWindowViewModel viewModel;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
        {
            settings = viewModel.Settings;
            DataContext = viewModel;
            this.viewModel = viewModel;
            API = api;
            InitializePosition();
            InitializeComponent();

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

            pluginListView = (CollectionView)CollectionViewSource.GetDefaultView(Plugins.ItemsSource);
            pluginListView.Filter = PluginListFilter;

            pluginStoreView = (CollectionView)CollectionViewSource.GetDefaultView(StoreListBox.ItemsSource); 
            pluginStoreView.Filter = PluginStoreFilter;

            InitializePosition();
        }

        private void OnSelectPythonDirectoryClick(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog
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

        private void OnSelectDefaultBrowserClick(object sender, RoutedEventArgs e)
        {
            var browserWindow = new SelectBrowserWindow(settings);
            browserWindow.ShowDialog();
        }

        #endregion

        #region Hotkey

        private void OnHotkeyControlLoaded(object sender, RoutedEventArgs e)
        {
            _ = HotkeyControl.SetHotkeyAsync(viewModel.Settings.Hotkey, false);
        }

        private void OnHotkeyControlFocused(object sender, RoutedEventArgs e)
        {
            HotKeyMapper.RemoveHotkey(settings.Hotkey);
        }

        private void OnHotkeyControlFocusLost(object sender, RoutedEventArgs e)
        {
            if (HotkeyControl.CurrentHotkeyAvailable)
            {
                HotKeyMapper.SetHotkey(HotkeyControl.CurrentHotkey, HotKeyMapper.OnToggleHotkey);
                HotKeyMapper.RemoveHotkey(settings.Hotkey);
                settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
            }
            else
            {
                HotKeyMapper.SetHotkey(new HotkeyModel(settings.Hotkey), HotKeyMapper.OnToggleHotkey);
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

        private void OnEditCustomHotkeyClick(object sender, RoutedEventArgs e)
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

        private void OnAddCustomHotkeyClick(object sender, RoutedEventArgs e)
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
                        website.OpenInBrowserTab();
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

        private void OnCheckUpdates(object sender, RoutedEventArgs e)
        {
            viewModel.UpdateApp(); // TODO: change to command
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            settings.SettingWindowState = WindowState;
            settings.SettingWindowTop = Top;
            settings.SettingWindowLeft = Left;
            viewModel.Save();
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void OpenThemeFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Themes));
        }

        private void OpenSettingFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Settings));
        }

        private void OpenWelcomeWindow(object sender, RoutedEventArgs e)
        {
            var WelcomeWindow = new WelcomeWindow(settings);
            WelcomeWindow.ShowDialog();
        }
        private void OpenLogFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Logs, Constant.Version));
        }
        private void ClearLogFolder(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show(
                InternationalizationManager.Instance.GetTranslation("clearlogfolderMessage"),
                InternationalizationManager.Instance.GetTranslation("clearlogfolder"), 
                MessageBoxButton.YesNo);
            
            if (confirmResult == MessageBoxResult.Yes)
            {
                viewModel.ClearLogFolder();
                
                ClearLogFolderBtn.Content = viewModel.CheckLogFolder;
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
        
        private void OnExternalPluginInstallClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: PluginStoreItemViewModel plugin } button)
            {
                return;
            }

            if (storeClickedButton != null)
            {
                FlyoutService.GetFlyout(storeClickedButton).Hide();
            }

            viewModel.DisplayPluginQuery($"install {plugin.Name}", PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7"));
        }

        private void OnExternalPluginUninstallClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var name = viewModel.SelectedPlugin.PluginPair.Metadata.Name;
                viewModel.DisplayPluginQuery($"uninstall {name}", PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7"));
            }


        }

        private void OnExternalPluginUninstallClick(object sender, RoutedEventArgs e)
        {
            if (storeClickedButton != null)
            {
                FlyoutService.GetFlyout(storeClickedButton).Hide();
            }

            if (sender is Button { DataContext: PluginStoreItemViewModel plugin })
                viewModel.DisplayPluginQuery($"uninstall {plugin.Name}", PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7"));

        }

        private void OnExternalPluginUpdateClick(object sender, RoutedEventArgs e)
        {
            if (storeClickedButton != null)
            {
                FlyoutService.GetFlyout(storeClickedButton).Hide();
            }
            if (sender is Button { DataContext: PluginStoreItemViewModel plugin })
                viewModel.DisplayPluginQuery($"update {plugin.Name}", PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7"));

        }

        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            if (Keyboard.FocusedElement is not TextBox textBox)
            {
                return;
            }
            var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            textBox.MoveFocus(tRequest);
        }

        private void ColorSchemeSelectedIndexChanged(object sender, EventArgs e)
            => ThemeManager.Current.ApplicationTheme = settings.ColorScheme switch
            {
                Constant.Light => ApplicationTheme.Light,
                Constant.Dark => ApplicationTheme.Dark,
                Constant.System => null,
                _ => ThemeManager.Current.ApplicationTheme
            };

        /* Custom TitleBar */

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {

            Close();
        }

        private void RefreshMaximizeRestoreButton()
        {
            if (WindowState == WindowState.Maximized)
            {
                maximizeButton.Visibility = Visibility.Collapsed;
                restoreButton.Visibility = Visibility.Visible;
            }
            else
            {
                maximizeButton.Visibility = Visibility.Visible;
                restoreButton.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            RefreshMaximizeRestoreButton();
        }

        #region Shortcut

        private void OnDeleteCustomShortCutClick(object sender, RoutedEventArgs e)
        {
            viewModel.DeleteSelectedCustomShortcut();
        }

        private void OnEditCustomShortCutClick(object sender, RoutedEventArgs e)
        {
            if (viewModel.EditSelectedCustomShortcut())
            {
                customShortcutView.Items.Refresh();
            }
        }

        private void OnAddCustomShortCutClick(object sender, RoutedEventArgs e)
        {
            viewModel.AddCustomShortcut();
        }

        #endregion
        
        private CollectionView pluginListView;
        private CollectionView pluginStoreView;

        private bool PluginListFilter(object item)
        {
            if (string.IsNullOrEmpty(pluginFilterTxb.Text))
                return true;
            if (item is PluginViewModel model)
            {
                return StringMatcher.FuzzySearch(pluginFilterTxb.Text, model.PluginPair.Metadata.Name).IsSearchPrecisionScoreMet();
            }
            return false;
        }

        private bool PluginStoreFilter(object item)
        {
            if (string.IsNullOrEmpty(pluginStoreFilterTxb.Text))
                return true;
            if (item is PluginStoreItemViewModel model)
            {
                return StringMatcher.FuzzySearch(pluginStoreFilterTxb.Text, model.Name).IsSearchPrecisionScoreMet()
                    || StringMatcher.FuzzySearch(pluginStoreFilterTxb.Text, model.Description).IsSearchPrecisionScoreMet();
            }
            return false;
        }

        private string lastPluginListSearch = "";
        private string lastPluginStoreSearch = "";

        private void RefreshPluginListEventHandler(object sender, RoutedEventArgs e)
        {
            if (pluginFilterTxb.Text != lastPluginListSearch)
            {
                lastPluginListSearch = pluginFilterTxb.Text;
                pluginListView.Refresh();
            }
        }

        private void RefreshPluginStoreEventHandler(object sender, RoutedEventArgs e)
        {
            if (pluginStoreFilterTxb.Text != lastPluginStoreSearch)
            {
                lastPluginStoreSearch = pluginStoreFilterTxb.Text;
                pluginStoreView.Refresh();
            }
        }

        private void PluginFilterTxb_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                RefreshPluginListEventHandler(sender, e);
        }

        private void PluginStoreFilterTxb_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                RefreshPluginStoreEventHandler(sender, e);
        }

        private void OnPluginSettingKeydown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.F)
                pluginFilterTxb.Focus();
        }

        private void PluginStore_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                pluginStoreFilterTxb.Focus();
            }
        }

        public void InitializePosition()
        {
            if (settings.SettingWindowTop >= 0 && settings.SettingWindowLeft >= 0)
            {
                Top = settings.SettingWindowTop;
                Left = settings.SettingWindowLeft;
            }
            else
            {
                Top = WindowTop();
                Left = WindowLeft();
            }
            WindowState = settings.SettingWindowState;
        }

        public double WindowLeft()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - this.ActualWidth) / 2 + dip1.X;
            return left;
        }

        public double WindowTop()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - this.ActualHeight) / 2 + dip1.Y - 20;
            return top;
        }

        private Button storeClickedButton;

        private void StoreListItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            storeClickedButton = button;

            var flyout = FlyoutService.GetFlyout(button);
            flyout.Closed += (_, _) =>
            {
                storeClickedButton = null;
            };

        }
    }
}
