using System;
using System.Collections.Generic;
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

        private static HWINEVENTHOOK _foregroundChangeHook = HWINEVENTHOOK.Null;

        private static HWINEVENTHOOK _locationChangeHook = HWINEVENTHOOK.Null;

        /*private static HWINEVENTHOOK _moveSizeHook = HWINEVENTHOOK.Null;*/

        private static HWINEVENTHOOK _destroyChangeHook = HWINEVENTHOOK.Null;

        private static DispatcherTimer _dragMoveTimer = null;

        // A list of all file dialog windows that are auto switched already
        private static readonly List<HWND> _autoSwitchedDialogs = new();

        private static readonly object _autoSwitchedDialogsLock = new();

        private static readonly SemaphoreSlim _navigationLock = new(1, 1);

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
                PInvoke.GetModuleHandle((PCWSTR)null),
                ForegroundChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            // Call LocationChange when the location of the window changes
            _locationChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                PInvoke.GetModuleHandle((PCWSTR)null),
                LocationChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            // Call MoveSizeCallBack when the window is moved or resized
            /*_moveSizeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_MOVESIZESTART,
                PInvoke.EVENT_SYSTEM_MOVESIZEEND,
                PInvoke.GetModuleHandle((PCWSTR)null),
                MoveSizeCallBack,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);*/

            // Call DestroyChange when the window is destroyed
            _destroyChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_DESTROY,
                PInvoke.EVENT_OBJECT_DESTROY,
                PInvoke.GetModuleHandle((PCWSTR)null),
                DestroyChangeCallback,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT);

            if (_foregroundChangeHook.IsNull ||
                _locationChangeHook.IsNull ||
                /*_moveSizeHook.IsNull ||*/
                _destroyChangeHook.IsNull)
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
                NavigateDialogPath(Win32Helper.GetForegroundWindowHWND());
            }
        }

        private static void NavigateDialogPath(HWND dialog, Action action = null)
        {
            if (dialog == HWND.Null || GetWindowClassName(dialog) != DialogWindowClassName) return;

            object document = null;
            try
            {
                lock (_lastExplorerViewLock)
                {
                    if (_lastExplorerView != null)
                    {
                        // Use dynamic here because using IWebBrower2.Document can cause exception here:
                        // System.Runtime.InteropServices.InvalidOleVariantTypeException: 'Specified OLE variant is invalid.'
                        dynamic explorerView = _lastExplorerView;
                        document = explorerView.Document;
                    }
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

            JumpToPath(dialog.Value, path, action);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "<Pending>")]
        public static void JumpToPath(nint dialog, string path, Action action = null)
        {
            if (!CheckPath(path, out var isFile)) return;

            var t = new Thread(async () =>
            {
                // Jump after flow launcher window vanished (after JumpAction returned true)
                // and the dialog had been in the foreground.
                var timeOut = !SpinWait.SpinUntil(() => Win32Helper.GetForegroundWindow() == dialog, 1000);
                if (timeOut)
                {
                    return;
                };

                // Assume that the dialog is in the foreground now
                await _navigationLock.WaitAsync();
                try
                {
                    var dialogHandle = new HWND(dialog);

                    bool result;
                    if (isFile)
                    {
                        result = Win32Helper.FileJump(path, dialogHandle);
                        Log.Debug(ClassName, $"File Jump: {path}");
                    }
                    else
                    {
                        result = Win32Helper.DirJump(path, dialogHandle);
                        Log.Debug(ClassName, $"Dir Jump: {path}");
                    }

                    if (result)
                    {
                        lock (_autoSwitchedDialogsLock)
                        {
                            _autoSwitchedDialogs.Add(dialogHandle);
                        }
                    }
                    else
                    {
                        Log.Error(ClassName, "Failed to jump to path");
                    }
                }
                catch (System.Exception e)
                {
                    Log.Exception(ClassName, "Failed to jump to path", e);
                }
                finally
                {
                    _navigationLock.Release();
                }
                
                // Invoke action if provided
                action?.Invoke();
            });
            t.Start();
            return;

            static bool CheckPath(string path, out bool file)
            {
                file = false;
                // Is non-null?
                if (string.IsNullOrEmpty(path)) return false;
                // Is absolute?
                if (!Path.IsPathRooted(path)) return false;
                // Is folder?
                var isFolder = Directory.Exists(path);
                // Is file?
                var isFile = File.Exists(path);
                file = isFile;
                return isFolder || isFile;
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
            // File dialog window
            if (GetWindowClassName(hwnd) == DialogWindowClassName)
            {
                Log.Debug(ClassName, $"Hwnd: {hwnd}");

                lock (_dialogWindowHandleLock)
                {
                    _dialogWindowHandle = hwnd;
                }

                // Navigate to path
                if (_settings.AutoQuickSwitch)
                {
                    // Check if we have already switched for this dialog
                    bool alreadySwitched;
                    lock (_autoSwitchedDialogsLock)
                    {
                        alreadySwitched = _autoSwitchedDialogs.Contains(hwnd);
                    }

                    // Just show quick switch window
                    if (alreadySwitched)
                    {
                        if (_settings.ShowQuickSwitchWindow)
                        {
                            ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                            _dragMoveTimer?.Start();
                        }
                    }
                    // Show quick switch window after navigating the path
                    else
                    {
                        NavigateDialogPath(hwnd, () =>
                        {
                            if (_settings.ShowQuickSwitchWindow)
                            {
                                ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                                _dragMoveTimer?.Start();
                            }
                        });
                    }
                }
                else
                {
                    // Show quick switch window
                    if (_settings.ShowQuickSwitchWindow)
                    {
                        ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                        _dragMoveTimer?.Start();
                    }
                }
            }
            // Quick switch window
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
            // If the dialog window is destroyed, set _dialogWindowHandle to null
            if (_dialogWindowHandle != HWND.Null && _dialogWindowHandle == hwnd)
            {
                Log.Debug(ClassName, $"Dialog Hwnd: {hwnd}");
                lock (_dialogWindowHandleLock)
                {
                    _dialogWindowHandle = HWND.Null;
                }
                lock (_autoSwitchedDialogsLock)
                {
                    _autoSwitchedDialogs.Remove(hwnd);
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
            // Reset initialize flag
            _isInitialized = false;

            // Dispose handle
            if (!_foregroundChangeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_foregroundChangeHook);
                _foregroundChangeHook = HWINEVENTHOOK.Null;
            }
            if (!_locationChangeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_locationChangeHook);
                _locationChangeHook = HWINEVENTHOOK.Null;
            }
            /*if (!_moveSizeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_moveSizeHook);
                _moveSizeHook = HWINEVENTHOOK.Null;
            }*/
            if (!_destroyChangeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_destroyChangeHook);
                _destroyChangeHook = HWINEVENTHOOK.Null;
            }

            // Release ComObjects
            if (_lastExplorerView != null)
            {
                Marshal.ReleaseComObject(_lastExplorerView);
                _lastExplorerView = null;
            }

            // Stop drag move timer
            if (_dragMoveTimer != null)
            {
                _dragMoveTimer.Stop();
                _dragMoveTimer = null;
            }
        }
    }
}
