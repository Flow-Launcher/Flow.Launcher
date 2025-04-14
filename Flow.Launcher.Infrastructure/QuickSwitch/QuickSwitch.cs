using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using NHotkey;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        private static readonly string ClassName = nameof(QuickSwitch);

        public static Action<nint> ShowQuickSwitchWindow { get; set; } = null;

        public static Action UpdateQuickSwitchWindow { get; set; } = null;

        public static Action ResetQuickSwitchWindow { get; set; } = null;

        // The class name of a dialog window
        private const string DialogWindowClassName = "#32770";

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        private static IWebBrowser2 _lastExplorerView = null;

        private static UnhookWinEventSafeHandle _foregroundChangeHook = null;

        private static UnhookWinEventSafeHandle _destroyChangeHook = null;

        private static UnhookWindowsHookExSafeHandle _callWndProcHook;

        private static HWND _dialogWindowHandle = HWND.Null;

        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // Check all foreground windows and check if there are explorer windows
            EnumerateShellWindows((shellWindow) =>
            {
                if (shellWindow is not IWebBrowser2 explorer)
                {
                    return;
                }

                _lastExplorerView = explorer;
            });

            // Call ForegroundChange when the foreground window changes
            _foregroundChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                null,
                ForegroundChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            // Call DestroyChange when the window is destroyed
            _destroyChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_DESTROY,
                PInvoke.EVENT_OBJECT_DESTROY,
                null,
                DestroyChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            // Install hook for dialog window message
            _callWndProcHook = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_CALLWNDPROC,
                CallWndProc,
                Process.GetCurrentProcess().SafeHandle,
                0);

            if (_foregroundChangeHook.IsInvalid ||
                _destroyChangeHook.IsInvalid ||
                _callWndProcHook.IsInvalid)
            {
                Log.Error(ClassName, "Failed to initialize QuickSwitch");
                return;
            }

            _isInitialized = true;
            return;
        }

        public static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            if (_isInitialized)
            {
                NavigateDialogPath();
            }
        }

        private static void NavigateDialogPath()
        {
            object document = null;
            try
            {
                if (_lastExplorerView != null)
                {
                    // Use dynamic here because using IWebBrower2.Document can cause exception here:
                    // System.Runtime.InteropServices.InvalidOleVariantTypeException: 'Specified OLE variant is invalid.'
                    dynamic explorerView = _lastExplorerView;
                    document = explorerView.Document;
                }
            }
            catch (COMException)
            {
                return;
            }

            if (document is not IShellFolderViewDual2 folderView)
            {
                return;
            }

            string path;
            try
            {
                // CSWin32 Folder does not have Self, so we need to use dynamic type here
                // Use dynamic to bypass static typing
                dynamic folder = folderView.Folder;

                // Access the Self property via dynamic binding
                dynamic folderItem = folder.Self;

                // Check if the item is part of the file system
                if (folderItem != null && folderItem.IsFileSystem)
                {
                    path = folderItem.Path;
                }
                else
                {
                    // Handle non-file system paths (e.g., virtual folders)
                    path = string.Empty;
                }
            }
            catch
            {
                return;
            }

            JumpToPath(path);
        }

        public static bool JumpToPath(string path)
        {
            if (!CheckPath(path)) return false;

            var t = new Thread(() =>
            {
                // Jump after flow launcher window vanished (after JumpAction returned true)
                // and the dialog had been in the foreground.
                var timeOut = !SpinWait.SpinUntil(() => GetWindowClassName(PInvoke.GetForegroundWindow()) == DialogWindowClassName, 1000);
                if (timeOut)
                {
                    return;
                };

                // Assume that the dialog is in the foreground now
                Win32Helper.DirJump(path, Win32Helper.GetForegroundWindow());
            });
            t.Start();
            return true;

            static bool CheckPath(string path)
            {
                // Is non-null
                if (string.IsNullOrEmpty(path)) return false;
                // Is absolute?
                if (!Path.IsPathRooted(path)) return false;
                // Is folder?
                if (!Directory.Exists(path)) return false;
                return true;
            }
        }

        private static string GetWindowClassName(HWND handle)
        {
            return GetClassName(handle);

            static unsafe string GetClassName(HWND handle)
            {
                fixed (char* buf = new char[256])
                {
                    return PInvoke.GetClassName(handle, buf, 256) switch
                    {
                        0 => null,
                        _ => new string(buf),
                    };
                }
            }
        }

        private static void ForegroundChangeCallback(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            // If window is dialog window, show quick switch window and navigate path if needed
            if (GetWindowClassName(hwnd) == DialogWindowClassName)
            {
                _dialogWindowHandle = hwnd;
                if (_settings.ShowQuickSwitchWindow)
                {
                    ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                }
                if (_settings.AutoQuickSwitch)
                {
                    // Showing quick switch window may bring focus
                    Win32Helper.SetForegroundWindow(hwnd);
                    NavigateDialogPath();
                }
            }

            // If window is explorer window, set _lastExplorerView to the explorer
            try
            {
                EnumerateShellWindows((shellWindow) =>
                {
                    try
                    {
                        if (shellWindow is not IWebBrowser2 explorer)
                        {
                            return;
                        }

                        if (explorer.HWND != hwnd.Value)
                        {
                            return;
                        }

                        _lastExplorerView = explorer;
                    }
                    catch (COMException)
                    {
                        // Ignored
                    }
                });
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to get shell windows", e);
            }
        }

        private static void DestroyChangeCallback(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            try
            {
                // If the explorer window is destroyed, set _lastExplorerView to null
                if (_lastExplorerView != null && _lastExplorerView.HWND == hwnd.Value)
                {
                    _lastExplorerView = null;
                }
            }
            catch (COMException)
            {
                // Ignored
            }

            // If the dialog window is destroyed, set _dialogWindowHandle to null
            if (_dialogWindowHandle != HWND.Null && _dialogWindowHandle == hwnd)
            {
                _dialogWindowHandle = HWND.Null;
                ResetQuickSwitchWindow?.Invoke();
            }
        }

        private static unsafe void EnumerateShellWindows(Action<object> action)
        {
            // Create an instance of ShellWindows
            var clsidShellWindows = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"); // ShellWindowsClass
            var iidIShellWindows = typeof(IShellWindows).GUID; // IShellWindows

            var result = PInvoke.CoCreateInstance(
                &clsidShellWindows,
                null,
                CLSCTX.CLSCTX_ALL,
                &iidIShellWindows,
                out var shellWindowsObj);

            if (result.Failed) return;

            var shellWindows = (IShellWindows)shellWindowsObj;

            // Enumerate the shell windows
            var count = shellWindows.Count;
            for (var i = 0; i < count; i++)
            {
                action(shellWindows.Item(i));
            }
        }

        private static LRESULT CallWndProc(int nCode, WPARAM wParam, LPARAM lParam)
        {
            if (nCode == PInvoke.HC_ACTION)
            {
                var msg = Marshal.PtrToStructure<CWPSTRUCT>(lParam);
                if (msg.hwnd == _dialogWindowHandle &&
                    (msg.message == PInvoke.WM_MOVE || msg.message == PInvoke.WM_SIZE))
                {
                    UpdateQuickSwitchWindow?.Invoke();
                }
            }

            return PInvoke.CallNextHookEx(
                _callWndProcHook,
                nCode,
                wParam,
                lParam);
        }

        public static void Dispose()
        {
            // Dispose handle
            if (_foregroundChangeHook != null)
            {
                _foregroundChangeHook.Dispose();
                _foregroundChangeHook = null;
            }
            if (_destroyChangeHook != null)
            {
                _destroyChangeHook.Dispose();
                _destroyChangeHook = null;
            }
            if (_callWndProcHook != null)
            {
                _callWndProcHook.Dispose();
                _callWndProcHook = null;
            }

            // Release ComObjects
            if (_lastExplorerView != null)
            {
                Marshal.ReleaseComObject(_lastExplorerView);
                _lastExplorerView = null;
            }
        }
    }
}
