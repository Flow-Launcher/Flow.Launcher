using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Interop.UIAutomationClient;
using NHotkey;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Shell;
using WindowsInput;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        private static readonly string ClassName = nameof(QuickSwitch);

        public static Action<nint> ShowQuickSwitchWindow { get; set; } = null;

        public static Action UpdateQuickSwitchWindow { get; set; } = null;

        public static Action DestoryQuickSwitchWindow { get; set; } = null;

        // The class name of a dialog window
        private const string DialogWindowClassName = "#32770";

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        private static CUIAutomation8 _automation = new CUIAutomation8Class();

        private static IWebBrowser2 _lastExplorerView = null;

        private static readonly InputSimulator _inputSimulator = new();

        private static UnhookWinEventSafeHandle _foregroundChangeHook = null;
        
        private static UnhookWinEventSafeHandle _locationChangeHook = null;

        private static UnhookWinEventSafeHandle _destroyChangeHook = null;

        private static HWND _dialogWindowHandle = HWND.Null;

        private static bool _isInitialized = false;

        public static bool Initialize()
        {
            if (_isInitialized) return true;

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

            // Call LocationChange when the location of the window changes
            _locationChangeHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                null,
                LocationChangeCallback,
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

            if (_foregroundChangeHook.IsInvalid || _locationChangeHook.IsInvalid || _destroyChangeHook.IsInvalid)
            {
                Log.Error(ClassName, "Failed to initialize QuickSwitch");
                return false;
            }

            _isInitialized = true;
            return true;
        }

        public static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            NavigateDialogPath();
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

                if (!Path.IsPathRooted(path))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            JumpToPath(path);
        }

        private static bool JumpToPath(string path)
        {
            var t = new Thread(() =>
            {
                // Jump after flow launcher window vanished (after JumpAction returned true)
                // and the dialog had been in the foreground.
                var timeOut = !SpinWait.SpinUntil(() => GetForegroundWindowClassName() == DialogWindowClassName, 1000);
                if (timeOut)
                {
                    return;
                };

                // Assume that the dialog is in the foreground now
                Win32Helper.DirJump(_inputSimulator, path, Win32Helper.GetForegroundWindow());
            });
            t.Start();
            return true;

            static string GetForegroundWindowClassName()
            {
                var handle = PInvoke.GetForegroundWindow();
                return GetClassName(handle);
            }
        }

        private static unsafe string GetClassName(HWND handle)
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
            IUIAutomationElement window;
            try
            {
                window = _automation.ElementFromHandle(hwnd);
            }
            catch
            {
                return;
            }

            // If window is dialog window, show quick switch window and navigate path if needed
            if (window is { CurrentClassName: DialogWindowClassName })
            {
                if (_settings.ShowQuickSwitchWindow)
                {
                    _dialogWindowHandle = hwnd;
                    ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                }
                if (_settings.AutoQuickSwitch)
                {
                    NavigateDialogPath();
                }
            }

            // If window is explorer window, set _lastExplorerView to the explorer
            try
            {
                EnumerateShellWindows((shellWindow) =>
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
                });
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to get shell windows", e);
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
            if (_dialogWindowHandle != null && _dialogWindowHandle == hwnd)
            {
                UpdateQuickSwitchWindow?.Invoke();
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
            // If the explorer window is destroyed, set _lastExplorerView to null
            if (_lastExplorerView != null && _lastExplorerView.HWND == hwnd.Value)
            {
                _lastExplorerView = null;
            }

            // If the dialog window is destroyed, set _dialogWindowHandle to null
            if (_dialogWindowHandle != null && _dialogWindowHandle == hwnd)
            {
                _dialogWindowHandle = HWND.Null;
                DestoryQuickSwitchWindow?.Invoke();
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
            if (_automation != null)
            {
                Marshal.ReleaseComObject(_automation);
                _automation = null;
            }
        }
    }
}
