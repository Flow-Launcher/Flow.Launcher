using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using NHotkey;
using NHotkey.Wpf;
using Flow.Launcher.Core.Resource;
using System.Windows;
using System.Windows.Input;
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
            settings = mainViewModel.Settings;

            SetHotkey(settings.Hotkey, OnToggleHotkey);
            LoadCustomPluginHotkey();
        }

        internal static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            if (!mainViewModel.ShouldIgnoreHotkeys())
                mainViewModel.ToggleFlowLauncher();
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
                HotkeyManager.Current.AddOrReplace(hotkeyStr, (Key)hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg =
                    string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"),
                        hotkeyStr);
                string errorMsgTitle = InternationalizationManager.Instance.GetTranslation("MessageBoxTitle");
                MessageBox.Show(errorMsg, errorMsgTitle);
            }
        }

        internal static void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        internal static void LoadCustomPluginHotkey()
        {
            if (settings.CustomPluginHotkeys == null)
                return;

            foreach (CustomPluginHotkey hotkey in settings.CustomPluginHotkeys)
            {
                SetCustomQueryHotkey(hotkey);
            }
        }

        internal static void SetCustomQueryHotkey(CustomPluginHotkey hotkey)
        {
            SetHotkey(hotkey.Hotkey, (s, e) =>
            {
                if (mainViewModel.ShouldIgnoreHotkeys())
                    return;

                mainViewModel.Show();
                mainViewModel.ChangeQueryText(hotkey.ActionKeyword, true);
            });
        }

        internal static bool CheckAvailability(HotkeyModel currentHotkey)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace("HotkeyAvailabilityTest", (Key)currentHotkey.CharKey,
                    currentHotkey.ModifierKeys, (sender, e) => { });

                return true;
            }
            catch
            {
            }
            finally
            {
                HotkeyManager.Current.Remove("HotkeyAvailabilityTest");
            }

            return false;
        }
    }
}
