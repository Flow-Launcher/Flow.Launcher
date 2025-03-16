using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Graphics.Dwm;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure
{
    public static class Win32Helper
    {
        #region Blur Handling

        public static bool IsBackdropSupported()
        {
            // Windows 11 (22000) 이상에서만 Mica 및 Acrylic 효과 지원
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Environment.OSVersion.Version.Build >= 22000;
        }

        public static unsafe bool DWMSetCloakForWindow(Window window, bool cloak)
        {
            var windowHelper = new WindowInteropHelper(window);
            windowHelper.EnsureHandle();

            var cloaked = cloak ? 1 : 0;

            return PInvoke.DwmSetWindowAttribute(
                new(windowHelper.Handle),
                DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                &cloaked,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        public static unsafe bool DWMSetBackdropForWindow(Window window, BackdropTypes backdrop)
        {
            var windowHelper = new WindowInteropHelper(window);
            windowHelper.EnsureHandle();

            var backdropType = backdrop switch
            {
                BackdropTypes.Acrylic => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                BackdropTypes.Mica => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                BackdropTypes.MicaAlt => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_AUTO
            };

            return PInvoke.DwmSetWindowAttribute(
                new(windowHelper.Handle),
                DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                &backdropType,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        public static unsafe bool DWMSetDarkModeForWindow(Window window, bool useDarkMode)
        {
            var windowHelper = new WindowInteropHelper(window);
            windowHelper.EnsureHandle();

            var darkMode = useDarkMode ? 1 : 0;

            return PInvoke.DwmSetWindowAttribute(
                new(windowHelper.Handle),
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                &darkMode,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="window"></param>
        /// <param name="cornerType">DoNotRound, Round, RoundSmall, Default</param>
        /// <returns></returns>
        public static unsafe bool DWMSetCornerPreferenceForWindow(Window window, string cornerType)
        {
            var windowHelper = new WindowInteropHelper(window);
            windowHelper.EnsureHandle();

            var preference = cornerType switch
            {
                "DoNotRound" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND,
                "Round" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND,
                "RoundSmall" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL,
                "Default" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT,
                _ => throw new InvalidOperationException("Invalid corner type")
            };

            return PInvoke.DwmSetWindowAttribute(
                new(windowHelper.Handle),
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &preference,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        #endregion
    }
}
