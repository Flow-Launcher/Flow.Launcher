using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowsInput;
using WindowsInput.Native;
using Point = System.Windows.Point;
using SystemFonts = System.Windows.SystemFonts;

namespace Flow.Launcher.Infrastructure
{
    public static class Win32Helper
    {
        #region Blur Handling

        public static bool IsBackdropSupported()
        {
            // Mica and Acrylic only supported Windows 11 22000+
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Environment.OSVersion.Version.Build >= 22000;
        }

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

        #region Wallpaper

        public static unsafe string GetWallpaperPath()
        {
            var wallpaperPtr = stackalloc char[(int)PInvoke.MAX_PATH];
            PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDESKWALLPAPER, PInvoke.MAX_PATH,
                wallpaperPtr,
                0);
            var wallpaper = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(wallpaperPtr);

            return wallpaper.ToString();
        }

        #endregion

        #region Window Foreground

        public static nint GetForegroundWindow()
        {
            return GetForegroundWindowHWND().Value;
        }

        internal static HWND GetForegroundWindowHWND()
        {
            return PInvoke.GetForegroundWindow();
        }

        public static bool SetForegroundWindow(Window window)
        {
            return PInvoke.SetForegroundWindow(GetWindowHandle(window));
        }

        public static bool SetForegroundWindow(nint handle)
        {
            return PInvoke.SetForegroundWindow(new(handle));
        }

        public static bool IsForegroundWindow(Window window)
        {
            return IsForegroundWindow(GetWindowHandle(window));
        }

        public static bool IsForegroundWindow(nint handle)
        {
            return IsForegroundWindow(new HWND(handle));
        }

        internal static bool IsForegroundWindow(HWND handle)
        {
            return handle.Equals(PInvoke.GetForegroundWindow());
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

        private static int GetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
        {
            var style = PInvoke.GetWindowLong(hWnd, nIndex);
            if (style == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            return style;
        }

        private static nint SetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, int dwNewLong)
        {
            PInvoke.SetLastError(WIN32_ERROR.NO_ERROR); // Clear any existing error

            var result = PInvoke.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
            if (result == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return result;
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

            // For desktop (Progman or WorkerW, depends on the system), we have to check
            if (windowClass is WINDOW_CLASS_PROGMAN or WINDOW_CLASS_WORKERW)
            {
                var hWndDesktop = PInvoke.FindWindowEx(hWnd, HWND.Null, "SHELLDLL_DefView", null);
                hWndDesktop = PInvoke.FindWindowEx(hWndDesktop, HWND.Null, "SysListView32", "FolderView");
                if (hWndDesktop.Value != IntPtr.Zero)
                {
                    return false;
                }
            }

            var monitorInfo = MonitorInfo.GetNearestDisplayMonitor(hWnd);
            return (appBounds.bottom - appBounds.top) == monitorInfo.RectMonitor.Height &&
                   (appBounds.right - appBounds.left) == monitorInfo.RectMonitor.Width;
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

        #region WndProc

        public const int WM_ENTERSIZEMOVE = (int)PInvoke.WM_ENTERSIZEMOVE;
        public const int WM_EXITSIZEMOVE = (int)PInvoke.WM_EXITSIZEMOVE;

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

        #region Keyboard Layout

        private const string UserProfileRegistryPath = @"Control Panel\International\User Profile";

        // https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/70feba9f-294e-491e-b6eb-56532684c37f
        private const string EnglishLanguageTag = "en";

        private static readonly string[] ImeLanguageTags =
        {
            "zh", // Chinese
            "ja", // Japanese
            "ko", // Korean
        };

        private const uint KeyboardLayoutLoWord = 0xFFFF;

        // Store the previous keyboard layout
        private static HKL _previousLayout;

        /// <summary>
        /// Switches the keyboard layout to English if available.
        /// </summary>
        /// <param name="backupPrevious">If true, the current keyboard layout will be stored for later restoration.</param>
        /// <exception cref="Win32Exception">Thrown when there's an error getting the window thread process ID.</exception>
        public static unsafe void SwitchToEnglishKeyboardLayout(bool backupPrevious)
        {
            // Find an installed English layout
            var enHKL = FindEnglishKeyboardLayout();

            // No installed English layout found
            if (enHKL == HKL.Null) return;

            // Get the foreground window
            var hwnd = PInvoke.GetForegroundWindow();
            if (hwnd == HWND.Null) return;

            // Get the current foreground window thread ID
            var threadId = PInvoke.GetWindowThreadProcessId(hwnd);
            if (threadId == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

            // If the current layout has an IME mode, disable it without switching to another layout.
            // This is needed because for languages with IME mode, Flow Launcher just temporarily disables
            // the IME mode instead of switching to another layout.
            var currentLayout = PInvoke.GetKeyboardLayout(threadId);
            var currentLangId = (uint)currentLayout.Value & KeyboardLayoutLoWord;
            foreach (var imeLangTag in ImeLanguageTags)
            {
                var langTag = GetLanguageTag(currentLangId);
                if (langTag.StartsWith(imeLangTag, StringComparison.OrdinalIgnoreCase)) return;
            }

            // Backup current keyboard layout
            if (backupPrevious) _previousLayout = currentLayout;

            // Switch to English layout
            PInvoke.ActivateKeyboardLayout(enHKL, 0);
        }

        /// <summary>
        /// Restores the previously backed-up keyboard layout.
        /// If it wasn't backed up or has already been restored, this method does nothing.
        /// </summary>
        public static void RestorePreviousKeyboardLayout()
        {
            if (_previousLayout == HKL.Null) return;

            var hwnd = PInvoke.GetForegroundWindow();
            if (hwnd == HWND.Null) return;

            PInvoke.PostMessage(
                hwnd,
                PInvoke.WM_INPUTLANGCHANGEREQUEST,
                PInvoke.INPUTLANGCHANGE_FORWARD,
                _previousLayout.Value
            );

            _previousLayout = HKL.Null;
        }

        /// <summary>
        /// Finds an installed English keyboard layout.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Win32Exception"></exception>
        private static unsafe HKL FindEnglishKeyboardLayout()
        {
            // Get the number of keyboard layouts
            int count = PInvoke.GetKeyboardLayoutList(0, null);
            if (count <= 0) return HKL.Null;

            // Get all keyboard layouts
            var handles = new HKL[count];
            fixed (HKL* h = handles)
            {
                var result = PInvoke.GetKeyboardLayoutList(count, h);
                if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // Look for any English keyboard layout
            foreach (var hkl in handles)
            {
                // The lower word contains the language identifier
                var langId = (uint)hkl.Value & KeyboardLayoutLoWord;
                var langTag = GetLanguageTag(langId);

                // Check if it's an English layout
                if (langTag.StartsWith(EnglishLanguageTag, StringComparison.OrdinalIgnoreCase))
                {
                    return hkl;
                }
            }

            return HKL.Null;
        }

        /// <summary>
        ///  Returns the
        ///  <see href="https://learn.microsoft.com/globalization/locale/standard-locale-names">
        ///   BCP 47 language tag
        ///  </see>
        ///  of the current input language.
        /// </summary>
        /// <remarks>
        /// Edited from: https://github.com/dotnet/winforms
        /// </remarks>
        private static string GetLanguageTag(uint langId)
        {
            // We need to convert the language identifier to a language tag, because they are deprecated and may have a
            // transient value.
            // https://learn.microsoft.com/globalization/locale/other-locale-names#lcid
            // https://learn.microsoft.com/windows/win32/winmsg/wm-inputlangchange#remarks
            //
            // It turns out that the LCIDToLocaleName API, which is used inside CultureInfo, may return incorrect
            // language tags for transient language identifiers. For example, it returns "nqo-GN" and "jv-Java-ID"
            // instead of the "nqo" and "jv-Java" (as seen in the Get-WinUserLanguageList PowerShell cmdlet).
            //
            // Try to extract proper language tag from registry as a workaround approved by a Windows team.
            // https://github.com/dotnet/winforms/pull/8573#issuecomment-1542600949
            //
            // NOTE: this logic may break in future versions of Windows since it is not documented.
            if (langId is PInvoke.LOCALE_TRANSIENT_KEYBOARD1
                or PInvoke.LOCALE_TRANSIENT_KEYBOARD2
                or PInvoke.LOCALE_TRANSIENT_KEYBOARD3
                or PInvoke.LOCALE_TRANSIENT_KEYBOARD4)
            {
                using var key = Registry.CurrentUser.OpenSubKey(UserProfileRegistryPath);
                if (key?.GetValue("Languages") is string[] languages)
                {
                    foreach (string language in languages)
                    {
                        using var subKey = key.OpenSubKey(language);
                        if (subKey?.GetValue("TransientLangId") is int transientLangId
                            && transientLangId == langId)
                        {
                            return language;
                        }
                    }
                }
            }

            return CultureInfo.GetCultureInfo((int)langId).Name;
        }

        #endregion

        #region Notification

        public static bool IsNotificationSupported()
        {
            // Notifications only supported on Windows 10 19041+
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Environment.OSVersion.Version.Build >= 19041;
        }

        #endregion

        #region Korean IME

        public static bool IsWindows11()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Environment.OSVersion.Version.Build >= 22000;
        }

        public static bool IsKoreanIMEExist()
        {
            return GetLegacyKoreanIMERegistryValue() != null;
        }

        public static bool IsLegacyKoreanIMEEnabled()
        {
            object value = GetLegacyKoreanIMERegistryValue();

            if (value is int intValue)
            {
                return intValue == 1;
            }
            else if (value != null && int.TryParse(value.ToString(), out int parsedValue))
            {
                return parsedValue == 1;
            }

            return false;
        }

        public static bool SetLegacyKoreanIMEEnabled(bool enable)
        {
            const string subKeyPath = @"Software\Microsoft\input\tsf\tsf3override\{A028AE76-01B1-46C2-99C4-ACD9858AE02F}";
            const string valueName = "NoTsf3Override5";

            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
                if (key != null)
                {
                    int value = enable ? 1 : 0;
                    key.SetValue(valueName, value, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch (System.Exception)
            {
                // Ignored
            }

            return false;
        }

        public static object GetLegacyKoreanIMERegistryValue()
        {
            const string subKeyPath = @"Software\Microsoft\input\tsf\tsf3override\{A028AE76-01B1-46C2-99C4-ACD9858AE02F}";
            const string valueName = "NoTsf3Override5";

            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath);
                if (key != null)
                {
                    return key.GetValue(valueName);
                }
            }
            catch (System.Exception)
            {
                // Ignored
            }

            return null;
        }

        public static void OpenImeSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:regionlanguage") { UseShellExecute = true });
            }
            catch (System.Exception)
            {
                // Ignored
            }
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

        public static string GetSystemDefaultFont()
        {
            try
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

        #region Quick Switch

        // Edited from: https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump

        internal static bool FileJump(string filePath, HWND dialogHandle, bool forceFileName = false, bool openFile = false)
        {
            if (forceFileName)
            {
                return DirFileJumpForFileName(filePath, dialogHandle, openFile);
            }
            else
            {
                return DirFileJump(Path.GetDirectoryName(filePath), filePath, dialogHandle);
            }
        }

        internal static bool DirJump(string dirPath, HWND dialogHandle)
        {
            return DirFileJump(dirPath, null, dialogHandle);
        }

        private static unsafe bool DirFileJump(string dirPath, string filePath, HWND dialogHandle)
        {
            // Get the handle of the path input box and then set the text.
            var controlHandle = PInvoke.GetDlgItem(dialogHandle, 0x0000); // WorkerW
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0xA005); // ReBarWindow32
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0xA205); // Address Band Root
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0x0000); // msctls_progress32
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0xA205); // ComboBoxEx32
            if (controlHandle == HWND.Null)
            {
                // https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump/issues/1
                // The dialog is a legacy one, so we edit file name text box directly.
                return DirFileJumpForFileName(string.IsNullOrEmpty(filePath) ? dirPath : filePath, dialogHandle, true);
            }

            var timeOut = !SpinWait.SpinUntil(() =>
            {
                var style = PInvoke.GetWindowLong(controlHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                return (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
            }, 1000);
            if (timeOut)
            {
                return false;
            }

            var editHandle = PInvoke.GetDlgItem(controlHandle, 0xA205); // ComboBox
            editHandle = PInvoke.GetDlgItem(editHandle, 0xA205); // Edit
            if (editHandle == HWND.Null)
            {
                return false;
            }

            SetWindowText(editHandle, dirPath);

            if (!string.IsNullOrEmpty(filePath))
            {
                // Note: I don't know why even openFile is set to false, the dialog still opens the file.
                return DirFileJumpForFileName(Path.GetFileName(filePath), dialogHandle, false);
            }

            return true;
        }

        /// <summary>
        /// Edit file name text box in the file open dialog.
        /// </summary>
        private static bool DirFileJumpForFileName(string fileName, HWND dialogHandle, bool openFile)
        {
            var controlHandle = PInvoke.GetDlgItem(dialogHandle, 0x047C); // ComboBoxEx32
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0x047C); // ComboBox
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0x047C); // Edit
            if (controlHandle == HWND.Null)
            {
                return false;
            }

            SetWindowText(controlHandle, fileName);

            if (openFile)
            {
                var openHandle = PInvoke.GetDlgItem(dialogHandle, 0x0001); // "&Open" Button
                if (openHandle == HWND.Null)
                {
                    return false;
                }

                ClickButton(openHandle);
            }

            return true;
        }

        private static unsafe nint SetWindowText(HWND handle, string text)
        {
            fixed (char* textPtr = text + '\0')
            {
                return PInvoke.SendMessage(handle, PInvoke.WM_SETTEXT, 0, (nint)textPtr).Value;
            }
        }

        private static unsafe nint ClickButton(HWND handle)
        {
            return PInvoke.PostMessage(handle, PInvoke.BM_CLICK, 0, 0).Value;
        }

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
    }
}
