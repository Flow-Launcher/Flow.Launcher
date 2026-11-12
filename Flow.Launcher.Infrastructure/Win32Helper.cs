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
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Power;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static partial class Win32Helper
    {
        #region Blur Handling

        public static bool IsBackdropSupported()
        {
            // Mica and Acrylic only supported Windows 11 22000+
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Environment.OSVersion.Version.Build >= 22000;
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

        public static bool SetForegroundWindow(nint handle)
        {
            return PInvoke.SetForegroundWindow(new(handle));
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

        #region WndProc

        public const int WM_ENTERSIZEMOVE = (int)PInvoke.WM_ENTERSIZEMOVE;
        public const int WM_EXITSIZEMOVE = (int)PInvoke.WM_EXITSIZEMOVE;
        public const int WM_NCLBUTTONDBLCLK = (int)PInvoke.WM_NCLBUTTONDBLCLK;
        public const int WM_SYSCOMMAND = (int)PInvoke.WM_SYSCOMMAND;

        public const int SC_MAXIMIZE = (int)PInvoke.SC_MAXIMIZE;
        public const int SC_MINIMIZE = (int)PInvoke.SC_MINIMIZE;

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
                var hrFolder = PInvoke.SHParseDisplayName(folderPath, null, out pidlFolder, 0, out _);
                if (hrFolder.Failed) throw new COMException("Failed to parse folder path", hrFolder);

                var hrFile = PInvoke.SHParseDisplayName(filePath, null, out pidlFile, 0, out _);
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

        #region Taskbar

        public static unsafe void ShowTaskbar()
        {
            // Find the taskbar window
            var taskbarHwnd = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "Shell_TrayWnd", null);
            if (taskbarHwnd == HWND.Null) return;

            // Magic from https://github.com/Oliviaophia/SmartTaskbar
            const uint TrayBarFlag = 0x05D1;
            var mon = PInvoke.MonitorFromWindow(taskbarHwnd, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            PInvoke.PostMessage(taskbarHwnd, TrayBarFlag, new WPARAM(1), new LPARAM((nint)mon.Value));
        }

        public static void HideTaskbar()
        {
            // Find the taskbar window
            var taskbarHwnd = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "Shell_TrayWnd", null);
            if (taskbarHwnd == HWND.Null) return;

            // Magic from https://github.com/Oliviaophia/SmartTaskbar
            const uint TrayBarFlag = 0x05D1;
            PInvoke.PostMessage(taskbarHwnd, TrayBarFlag, new WPARAM(0), IntPtr.Zero);
        }

        #endregion
    }
}
