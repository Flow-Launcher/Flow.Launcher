using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using NHotkey;
using NHotkey.Wpf;
using Flow.Launcher.Core.Resource;
using System.Windows;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Helper
{
    internal static class HotKeyMapper
    {
        private static Settings settings;
        private static MainViewModel mainViewModel;

        internal static void Initialize(MainViewModel mainVM)
        {
            mainViewModel = mainVM;
            settings = mainViewModel._settings;

            SetHotkey(settings.Hotkey, OnHotkey);
            SetCustomPluginHotkey();
        }

        private static void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hotkey, action);
        }

        internal static void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg =
                    string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"),
                        hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        internal static void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        internal static void OnHotkey(object sender, HotkeyEventArgs e)
        {
            if (!ShouldIgnoreHotkeys())
            {
                if (settings.LastQueryMode == LastQueryMode.Empty)
                {
                    mainViewModel.ChangeQueryText(string.Empty);
                }
                else if (settings.LastQueryMode == LastQueryMode.Preserved)
                {
                    mainViewModel.LastQuerySelected = true;
                }
                else if (settings.LastQueryMode == LastQueryMode.Selected)
                {
                    mainViewModel.LastQuerySelected = false;
                }
                else
                {
                    throw new ArgumentException($"wrong LastQueryMode: <{settings.LastQueryMode}>");
                }

                mainViewModel.ToggleFlowLauncher();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Checks if Flow Launcher should ignore any hotkeys
        /// </summary>
        private static bool ShouldIgnoreHotkeys()
        {
            return settings.IgnoreHotkeysOnFullscreen && WindowsInteropHelper.IsWindowFullscreen();
        }

        private static void SetCustomPluginHotkey()
        {
            if (settings.CustomPluginHotkeys == null) return;
            foreach (CustomPluginHotkey hotkey in settings.CustomPluginHotkeys)
            {
                SetHotkey(hotkey.Hotkey, (s, e) =>
                {
                    if (ShouldIgnoreHotkeys()) return;
                    mainViewModel.MainWindowVisibility = Visibility.Visible;
                    mainViewModel.ChangeQueryText(hotkey.ActionKeyword);
                });
            }
        }
    }
}
