using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Point = System.Windows.Point;
using SystemFonts = System.Windows.SystemFonts;

namespace Flow.Launcher.Infrastructure
{
    public static partial class Win32Helper
    {
        #region Blur Handling

        public static unsafe bool DWMSetCloakForWindow(Window window, bool cloak)
        {
            var cloaked = cloak ? 1 : 0;

            return PInvoke.DwmSetWindowAttribute(
                GetWindowHandle(window),
                DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
                &cloaked,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        public static unsafe bool DWMSetBackdropForWindow(Window window, BackdropTypes backdrop)
        {
            var backdropType = backdrop switch
            {
                BackdropTypes.Acrylic => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                BackdropTypes.Mica => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                BackdropTypes.MicaAlt => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_AUTO
            };

            // The backdrop renders in the non-client frame area. WindowStyle=None
            // removes that area, so we extend it across the entire client area.
            // This is harmless when backdrop is None or blur not enabled
            // as DWM has nothing to render in the extended area.
            // https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            PInvoke.DwmExtendFrameIntoClientArea(GetWindowHandle(window), in margins);

            return PInvoke.DwmSetWindowAttribute(
                GetWindowHandle(window),
                DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                &backdropType,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        public static unsafe bool DWMSetDarkModeForWindow(Window window, bool useDarkMode)
        {
            var darkMode = useDarkMode ? 1 : 0;

            return PInvoke.DwmSetWindowAttribute(
                GetWindowHandle(window),
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
            var preference = cornerType switch
            {
                "DoNotRound" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND,
                "Round" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND,
                "RoundSmall" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL,
                "Default" => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT,
                _ => throw new InvalidOperationException("Invalid corner type")
            };

            return PInvoke.DwmSetWindowAttribute(
                GetWindowHandle(window),
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &preference,
                (uint)Marshal.SizeOf<int>()).Succeeded;
        }

        #endregion

        #region Window Foreground

        public static bool SetForegroundWindow(Window window)
        {
            return PInvoke.SetForegroundWindow(GetWindowHandle(window));
        }

        public static bool IsForegroundWindow(Window window)
        {
            return IsForegroundWindow(GetWindowHandle(window));
        }

        #endregion

        #region Task Switching

        /// <summary>
        /// Hide windows in the Alt+Tab window list
        /// </summary>
        /// <param name="window">To hide a window</param>
        public static void HideFromAltTab(Window window)
        {
            var hwnd = GetWindowHandle(window);

            var exStyle = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            // Add TOOLWINDOW style, remove APPWINDOW style
            var newExStyle = ((uint)exStyle | (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) & ~(uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW;

            SetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newExStyle);
        }

        /// <summary>
        /// Restore window display in the Alt+Tab window list.
        /// </summary>
        /// <param name="window">To restore the displayed window</param>
        public static void ShowInAltTab(Window window)
        {
            var hwnd = GetWindowHandle(window);

            var exStyle = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            // Remove the TOOLWINDOW style and add the APPWINDOW style.
            var newExStyle = ((uint)exStyle & ~(uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) | (uint)WINDOW_EX_STYLE.WS_EX_APPWINDOW;

            SetWindowStyle(GetWindowHandle(window), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newExStyle);
        }

        /// <summary>
        /// Disable windows toolbar's control box
        /// This will also disable system menu with Alt+Space hotkey
        /// </summary>
        public static void DisableControlBox(Window window)
        {
            var hwnd = GetWindowHandle(window);

            var style = GetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

            style &= ~(int)WINDOW_STYLE.WS_SYSMENU;

            SetWindowStyle(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
        }

        #endregion

        #region Window Fullscreen

        private const string WINDOW_CLASS_CONSOLE = "ConsoleWindowClass";
        private const string WINDOW_CLASS_WINTAB = "Flip3D";
        private const string WINDOW_CLASS_PROGMAN = "Progman";
        private const string WINDOW_CLASS_WORKERW = "WorkerW";

        private static HWND _hwnd_shell;
        private static HWND HWND_SHELL =>
            _hwnd_shell != HWND.Null ? _hwnd_shell : _hwnd_shell = PInvoke.GetShellWindow();

        private static HWND _hwnd_desktop;
        private static HWND HWND_DESKTOP =>
            _hwnd_desktop != HWND.Null ? _hwnd_desktop : _hwnd_desktop = PInvoke.GetDesktopWindow();

        public static unsafe bool IsForegroundWindowFullscreen()
        {
            // Get current active window
            var hWnd = PInvoke.GetForegroundWindow();
            if (hWnd.Equals(HWND.Null))
            {
                return false;
            }

            // If current active window is desktop or shell, exit early
            if (hWnd.Equals(HWND_DESKTOP) || hWnd.Equals(HWND_SHELL))
            {
                return false;
            }

            string windowClass;
            const int capacity = 256;
            Span<char> buffer = stackalloc char[capacity];
            int validLength;
            fixed (char* pBuffer = buffer)
            {
                validLength = PInvoke.GetClassName(hWnd, pBuffer, capacity);
            }

            windowClass = buffer[..validLength].ToString();

            // For Win+Tab (Flip3D)
            if (windowClass == WINDOW_CLASS_WINTAB)
            {
                return false;
            }

            PInvoke.GetWindowRect(hWnd, out var appBounds);

            // For console (ConsoleWindowClass), we have to check for negative dimensions
            if (windowClass == WINDOW_CLASS_CONSOLE)
            {
                return appBounds.top < 0 && appBounds.bottom < 0;
            }

            // For desktop (Progman or WorkerW, depends on the system),
            // we have to check for this hierarchy:
            // Progman/WorkerW -> SHELLDLL_DefView -> SysListView32("FolderView")
            if (windowClass is WINDOW_CLASS_PROGMAN or WINDOW_CLASS_WORKERW)
            {
                var hWndDefView = PInvoke.FindWindowEx(hWnd, HWND.Null, "SHELLDLL_DefView", null);
                if (hWndDefView != HWND.Null)
                {
                    var hWndFolderView = PInvoke.FindWindowEx(hWndDefView, HWND.Null, "SysListView32", "FolderView");
                    if (hWndFolderView != HWND.Null)
                    {
                        return false;
                    }
                }
            }

            var monitorInfo = MonitorInfo.GetNearestDisplayMonitor(hWnd);
            if (monitorInfo == null)
            {
                return false;
            }
            return (appBounds.bottom - appBounds.top) == monitorInfo.Bounds.Height &&
                   (appBounds.right - appBounds.left) == monitorInfo.Bounds.Width;
        }

        #endregion

        #region Pixel to DIP

        /// <summary>
        /// Transforms pixels to Device Independent Pixels used by WPF
        /// </summary>
        /// <param name="visual">current window, required to get presentation source</param>
        /// <param name="unitX">horizontal position in pixels</param>
        /// <param name="unitY">vertical position in pixels</param>
        /// <returns>point containing device independent pixels</returns>
        public static Point TransformPixelsToDIP(Visual visual, double unitX, double unitY)
        {
            Matrix matrix;
            var source = PresentationSource.FromVisual(visual);
            if (source is not null)
            {
                matrix = source.CompositionTarget.TransformFromDevice;
            }
            else
            {
                using var src = new HwndSource(new HwndSourceParameters());
                matrix = src.CompositionTarget.TransformFromDevice;
            }

            return new Point((int)(matrix.M11 * unitX), (int)(matrix.M22 * unitY));
        }

        #endregion

        #region Window Handle

        internal static HWND GetWindowHandle(Window window, bool ensure = false)
        {
            var windowHelper = new WindowInteropHelper(window);
            if (ensure)
            {
                windowHelper.EnsureHandle();
            }
            return new(windowHelper.Handle);
        }

        internal static HWND GetMainWindowHandle()
        {
            // When application is exiting, the Application.Current will be null
            if (Application.Current == null) return HWND.Null;

            // Get the FL main window
            var hwnd = GetWindowHandle(Application.Current.MainWindow, true);
            return hwnd;
        }

        #endregion

        #region System Font

        private static readonly Dictionary<string, string> _languageToNotoSans = new()
        {
            { "ko", "Noto Sans KR" },
            { "ja", "Noto Sans JP" },
            { "zh-CN", "Noto Sans SC" },
            { "zh-SG", "Noto Sans SC" },
            { "zh-Hans", "Noto Sans SC" },
            { "zh-TW", "Noto Sans TC" },
            { "zh-HK", "Noto Sans TC" },
            { "zh-MO", "Noto Sans TC" },
            { "zh-Hant", "Noto Sans TC" },
            { "th", "Noto Sans Thai" },
            { "ar", "Noto Sans Arabic" },
            { "he", "Noto Sans Hebrew" },
            { "hi", "Noto Sans Devanagari" },
            { "bn", "Noto Sans Bengali" },
            { "ta", "Noto Sans Tamil" },
            { "el", "Noto Sans Greek" },
            { "ru", "Noto Sans" },
            { "en", "Noto Sans" },
            { "fr", "Noto Sans" },
            { "de", "Noto Sans" },
            { "es", "Noto Sans" },
            { "pt", "Noto Sans" }
        };

        /// <summary>
        /// Gets the system default font.
        /// </summary>
        /// <param name="useNoto">
        /// If true, it will try to find the Noto font for the current culture.
        /// </param>
        /// <returns>
        /// The name of the system default font.
        /// </returns>
        public static string GetSystemDefaultFont(bool useNoto = true)
        {
            try
            {
                if (useNoto)
                {
                    var culture = CultureInfo.CurrentCulture;
                    var language = culture.Name; // e.g., "zh-TW"
                    var langPrefix = language.Split('-')[0]; // e.g., "zh"

                    // First, try to find by full name, and if not found, fallback to prefix
                    if (TryGetNotoFont(language, out var notoFont) || TryGetNotoFont(langPrefix, out notoFont))
                    {
                        // If the font is installed, return it
                        if (Fonts.SystemFontFamilies.Any(f => f.Source.Equals(notoFont)))
                        {
                            return notoFont;
                        }
                    }
                }

                // If Noto font is not found, fallback to the system default font
                var font = SystemFonts.MessageFontFamily;
                if (font.FamilyNames.TryGetValue(XmlLanguage.GetLanguage("en-US"), out var englishName))
                {
                    return englishName;
                }

                return font.Source ?? "Segoe UI";
            }
            catch
            {
                return "Segoe UI";
            }
        }

        private static bool TryGetNotoFont(string langKey, out string notoFont)
        {
            return _languageToNotoSans.TryGetValue(langKey, out notoFont);
        }

        #endregion

        #region Window Rect

        public static unsafe bool GetWindowRect(nint handle, out Rect outRect)
        {
            var rect = new RECT();
            var result = PInvoke.GetWindowRect(new(handle), &rect);
            if (!result)
            {
                outRect = new Rect();
                return false;
            }

            // Convert RECT to Rect
            outRect = new Rect(
                rect.left,
                rect.top,
                rect.right - rect.left,
                rect.bottom - rect.top
            );
            return true;
        }

        #endregion

        #region File / Folder Dialog

        public static string SelectFile()
        {
            var dlg = new OpenFileDialog();
            var result = dlg.ShowDialog();
            if (result == true)
                return dlg.FileName;

            return string.Empty;
        }

        #endregion
    }
}
