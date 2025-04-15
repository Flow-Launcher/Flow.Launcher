using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using NHotkey;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        private static readonly string ClassName = nameof(QuickSwitch);

        public static Action<nint> ShowQuickSwitchWindow { get; set; } = null;

        public static Action UpdateQuickSwitchWindow { get; set; } = null;

        public static Action ResetQuickSwitchWindow { get; set; } = null;

        public static Action HideQuickSwitchWindow { get; set; } = null;

        // The class name of a dialog window
        private const string DialogWindowClassName = "#32770";

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        private static readonly object _lastExplorerViewLock = new();

        private static readonly object _dialogWindowHandleLock = new();

        private static IWebBrowser2 _lastExplorerView = null;

        private static UnhookWinEventSafeHandle _foregroundChangeHook = null;

        private static UnhookWinEventSafeHandle _locationChangeHook = null;

        /*private static UnhookWinEventSafeHandle _moveSizeHook = null;*/

        private static UnhookWinEventSafeHandle _destroyChangeHook = null;

        private static DispatcherTimer _dragMoveTimer = null;

        private static HWND _mainWindowHandle = HWND.Null;

        private static HWND _dialogWindowHandle = HWND.Null;

        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // Check all foreground windows and check if there are explorer windows
            lock (_lastExplorerViewLock)
            {
                var explorerInitialized = false;
                EnumerateShellWindows((shellWindow) =>
                {
                    if (shellWindow is not IWebBrowser2 explorer)
                    {
                        return;
                    }

                    // Initialize one explorer window even if it is not foreground
                    if (!explorerInitialized)
                    {
                        _lastExplorerView = explorer;
                    }
                    // Force update explorer window if it is foreground
                    else if (Win32Helper.IsForegroundWindow(explorer.HWND.Value))
                    {
                        _lastExplorerView = explorer;
                    }
                });
            }

            // Call ForegroundChange when the foreground window changes
            _foregroundChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                null,
                ForegroundChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            // Call LocationChange when the location of the window changes
            _locationChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                null,
                LocationChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            // Call MoveSizeCallBack when the window is moved or resized
            /*_moveSizeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_MOVESIZESTART,
                PInvoke.EVENT_SYSTEM_MOVESIZEEND,
                null,
                MoveSizeCallBack,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);*/

            // Call DestroyChange when the window is destroyed
            _destroyChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_DESTROY,
                PInvoke.EVENT_OBJECT_DESTROY,
                null,
                DestroyChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            if (_foregroundChangeHook.IsInvalid ||
                _locationChangeHook.IsInvalid ||
                /*_moveSizeHook.IsInvalid ||*/
                _destroyChangeHook.IsInvalid)
            {
                Log.Error(ClassName, "Failed to initialize QuickSwitch");
                return;
            }

            // Initialize main window handle
            _mainWindowHandle = Win32Helper.GetMainWindowHandle();

            // Initialize timer
            _dragMoveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _dragMoveTimer.Tick += (s, e) => UpdateQuickSwitchWindow?.Invoke();

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
                // Is file?
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
            // File dialog window is foreground
            if (GetWindowClassName(hwnd) == DialogWindowClassName)
            {
                lock (_dialogWindowHandleLock)
                {
                    _dialogWindowHandle = hwnd;
                }

                // Show quick switch window
                if (_settings.ShowQuickSwitchWindow)
                {
                    ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                    _dragMoveTimer?.Start();
                }

                // Navigate path if needed
                if (_settings.AutoQuickSwitch)
                {
                    // Showing quick switch window may bring focus
                    Win32Helper.SetForegroundWindow(hwnd);
                    NavigateDialogPath();
                }
            }
            // Quick switch window is foreground
            else if (hwnd == _mainWindowHandle)
            {
                // Nothing to do
            }
            else
            {
                if (_dialogWindowHandle != HWND.Null)
                {
                    // Neither quick switch window nor file dialog window is foreground
                    // Hide quick switch window until the file dialog window is brought to the foreground
                    HideQuickSwitchWindow?.Invoke();
                }

                // Check if explorer window is foreground
                try
                {
                    lock (_lastExplorerViewLock)
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
                }
                catch (System.Exception)
                {
                    // Ignored
                }
            }
        }

        private static void LocationChangeCallback(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            // If the dialog window is moved, update the quick switch window position
            if (_dialogWindowHandle != HWND.Null && _dialogWindowHandle == hwnd)
            {
                UpdateQuickSwitchWindow?.Invoke();
            }
        }

        // TODO: Use a better way to detect dragging
        // Here we do not start & stop the timer beacause the start time is not accurate (more than 1s delay)
        // So we start & stop the timer when we find a file dialog window
        /*private static void MoveSizeCallBack(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            // If the dialog window is moved or resized, update the quick switch window position
            if (_dialogWindowHandle != HWND.Null && _dialogWindowHandle == hwnd && _dragMoveTimer != null)
            {
                switch (eventType)
                {
                    case PInvoke.EVENT_SYSTEM_MOVESIZESTART:
                        _dragMoveTimer.Start(); // Start dragging position
                        break;
                    case PInvoke.EVENT_SYSTEM_MOVESIZEEND:
                        _dragMoveTimer.Stop(); // Stop dragging
                        break;
                }
            }
        }*/

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
                lock (_lastExplorerViewLock)
                {
                    if (_lastExplorerView != null && _lastExplorerView.HWND == hwnd.Value)
                    {
                        _lastExplorerView = null;
                    }
                }
            }
            catch (COMException)
            {
                // Ignored
            }

            // If the dialog window is destroyed, set _dialogWindowHandle to null
            if (_dialogWindowHandle != HWND.Null && _dialogWindowHandle == hwnd)
            {
                lock (_dialogWindowHandleLock)
                {
                    _dialogWindowHandle = HWND.Null;
                }
                ResetQuickSwitchWindow?.Invoke();
                _dragMoveTimer?.Stop();
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

        public static void Dispose()
        {
            // Dispose handle
            if (_foregroundChangeHook != null)
            {
                _foregroundChangeHook.Dispose();
                _foregroundChangeHook = null;
            }
            if (_locationChangeHook != null)
            {
                _locationChangeHook.Dispose();
                _locationChangeHook = null;
            }
            /*if (_moveSizeHook != null)
            {
                _moveSizeHook.Dispose();
                _moveSizeHook = null;
            }*/
            if (_destroyChangeHook != null)
            {
                _destroyChangeHook.Dispose();
                _destroyChangeHook = null;
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
