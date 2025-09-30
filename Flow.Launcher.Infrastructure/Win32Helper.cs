using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Power;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;
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

        public static unsafe nint GetForegroundWindow()
        {
            return (nint)PInvoke.GetForegroundWindow().Value;
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

        private static nint GetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
        {
            var style = PInvoke.GetWindowLongPtr(hWnd, nIndex);
            if (style == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            return style;
        }

        private static nint SetWindowStyle(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong)
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
                if (hWndDesktop != HWND.Null)
                {
                    return false;
                }
            }

            var monitorInfo = MonitorInfo.GetNearestDisplayMonitor(hWnd);
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

        #region WndProc

        public const int WM_ENTERSIZEMOVE = (int)PInvoke.WM_ENTERSIZEMOVE;
        public const int WM_EXITSIZEMOVE = (int)PInvoke.WM_EXITSIZEMOVE;
        public const int WM_NCLBUTTONDBLCLK = (int)PInvoke.WM_NCLBUTTONDBLCLK;
        public const int WM_SYSCOMMAND = (int)PInvoke.WM_SYSCOMMAND;

        public const int SC_MAXIMIZE = (int)PInvoke.SC_MAXIMIZE;
        public const int SC_MINIMIZE = (int)PInvoke.SC_MINIMIZE;

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

        #region STA Thread

        /*
        Inspired by https://github.com/files-community/Files code on STA Thread handling.
        */

        public static Task StartSTATaskAsync(Action action)
        {
            var taskCompletionSource = new TaskCompletionSource();
            Thread thread = new(() =>
            {
                PInvoke.OleInitialize();

                try
                {
                    action();
                    taskCompletionSource.SetResult();
                }
                catch (System.Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
                finally
                {
                    PInvoke.OleUninitialize();
                }
            })
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return taskCompletionSource.Task;
        }

        public static Task<T> StartSTATaskAsync<T>(Func<T> func)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();

            Thread thread = new(() =>
            {
                PInvoke.OleInitialize();

                try
                {
                    taskCompletionSource.SetResult(func());
                }
                catch (System.Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
                finally
                {
                    PInvoke.OleUninitialize();
                }
            })
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return taskCompletionSource.Task;
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
        public unsafe static void RestorePreviousKeyboardLayout()
        {
            if (_previousLayout == HKL.Null) return;

            var hwnd = PInvoke.GetForegroundWindow();
            if (hwnd == HWND.Null) return;

            PInvoke.PostMessage(
                hwnd,
                PInvoke.WM_INPUTLANGCHANGEREQUEST,
                PInvoke.INPUTLANGCHANGE_FORWARD,
                new LPARAM((nint)_previousLayout.Value)
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

        #region Window Process

        internal static unsafe string GetProcessNameFromHwnd(HWND hWnd)
        {
            return Path.GetFileName(GetProcessPathFromHwnd(hWnd));
        }

        internal static unsafe string GetProcessPathFromHwnd(HWND hWnd)
        {
            uint pid;
            var threadId = PInvoke.GetWindowThreadProcessId(hWnd, &pid);
            if (threadId == 0) return string.Empty;

            var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process != HWND.Null)
            {
                using var safeHandle = new SafeProcessHandle((nint)process.Value, true);
                uint capacity = 2000;
                Span<char> buffer = new char[capacity];
                if (!PInvoke.QueryFullProcessImageName(safeHandle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref capacity))
                {
                    return string.Empty;
                }

                return buffer[..(int)capacity].ToString();
            }

            return string.Empty;
        }

        #endregion

        #region Explorer

        // https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems

        public static unsafe void OpenFolderAndSelectFile(string filePath)
        {
            ITEMIDLIST* pidlFolder = null;
            ITEMIDLIST* pidlFile = null;

            var folderPath = Path.GetDirectoryName(filePath);

            try
            {
                var hrFolder = PInvoke.SHParseDisplayName(folderPath, null, out pidlFolder, 0, null);
                if (hrFolder.Failed) throw new COMException("Failed to parse folder path", hrFolder);

                var hrFile = PInvoke.SHParseDisplayName(filePath, null, out pidlFile, 0, null);
                if (hrFile.Failed) throw new COMException("Failed to parse file path", hrFile);

                var hrSelect = PInvoke.SHOpenFolderAndSelectItems(pidlFolder, 1, &pidlFile, 0);
                if (hrSelect.Failed) throw new COMException("Failed to open folder and select item", hrSelect);
            }
            finally
            {
                if (pidlFile != null) PInvoke.CoTaskMemFree(pidlFile);
                if (pidlFolder != null) PInvoke.CoTaskMemFree(pidlFolder);
            }
        }

        #endregion

        #region Win32 Dark Mode

        /*
         * Inspired by https://github.com/ysc3839/win32-darkmode
         */

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        private static extern int SetPreferredAppMode(int appMode);

        public static void EnableWin32DarkMode(string colorScheme)
        {
            try
            {
                // Undocumented API from Windows 10 1809
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    Environment.OSVersion.Version.Build >= 17763)
                {
                    var flag = colorScheme switch
                    {
                        Constant.Light => 3, // ForceLight
                        Constant.Dark => 2, // ForceDark
                        Constant.System => 1, // AllowDark
                        _ => 0 // Default
                    };
                    _ = SetPreferredAppMode(flag);
                }

            }
            catch
            {
                // Ignore errors on unsupported OS
            }
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

        #region Sleep Mode Listener

        private static Action _func;
        private static PDEVICE_NOTIFY_CALLBACK_ROUTINE _callback = null;
        private static DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS _recipient;
        private static SafeHandle _recipientHandle;
        private static HPOWERNOTIFY _handle = HPOWERNOTIFY.Null;

        /// <summary>
        /// Registers a listener for sleep mode events.
        /// Inspired from: https://github.com/XKaguya/LenovoLegionToolkit
        /// https://blog.csdn.net/mochounv/article/details/114668594
        /// </summary>
        /// <param name="func"></param>
        /// <exception cref="Win32Exception"></exception>
        public static unsafe void RegisterSleepModeListener(Action func)
        {
            if (_callback != null)
            {
                // Only register if not already registered
                return;
            }

            _func = func;
            _callback = new PDEVICE_NOTIFY_CALLBACK_ROUTINE(DeviceNotifyCallback);
            _recipient = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS()
            {
                Callback = _callback,
                Context = null
            };

            _recipientHandle = new StructSafeHandle<DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS>(_recipient);
            _handle = PInvoke.PowerRegisterSuspendResumeNotification(
                REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_CALLBACK,
                _recipientHandle,
                out var handle) == WIN32_ERROR.ERROR_SUCCESS ?
                new HPOWERNOTIFY(new IntPtr(handle)) :
                HPOWERNOTIFY.Null;
            if (_handle.IsNull)
            {
                throw new Win32Exception("Error registering for power notifications: " + Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Unregisters the sleep mode listener.
        /// </summary>
        public static void UnregisterSleepModeListener()
        {
            if (!_handle.IsNull)
            {
                PInvoke.PowerUnregisterSuspendResumeNotification(_handle);
                _handle = HPOWERNOTIFY.Null;
                _func = null;
                _callback = null;
                _recipientHandle = null;
            }
        }

        private static unsafe uint DeviceNotifyCallback(void* context, uint type, void* setting)
        {
            switch (type)
            {
                case PInvoke.PBT_APMRESUMEAUTOMATIC:
                    // Operation is resuming automatically from a low-power state.This message is sent every time the system resumes
                    _func?.Invoke();
                    break;

                case PInvoke.PBT_APMRESUMESUSPEND:
                    // Operation is resuming from a low-power state.This message is sent after PBT_APMRESUMEAUTOMATIC if the resume is triggered by user input, such as pressing a key
                    _func?.Invoke();
                    break;
            }

            return 0;
        }

        private sealed class StructSafeHandle<T> : SafeHandle where T : struct
        {
            private readonly nint _ptr = nint.Zero;

            public StructSafeHandle(T recipient) : base(nint.Zero, true)
            {
                var pRecipient = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
                Marshal.StructureToPtr(recipient, pRecipient, false);
                SetHandle(pRecipient);
                _ptr = pRecipient;
            }

            public override bool IsInvalid => handle == nint.Zero;

            protected override bool ReleaseHandle()
            {
                Marshal.FreeHGlobal(_ptr);
                return true;
            }
        }

        #endregion
    }
}
