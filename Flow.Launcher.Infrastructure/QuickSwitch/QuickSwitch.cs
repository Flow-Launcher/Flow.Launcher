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

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        // The class name of a dialog window
        private const string DialogWindowClassName = "#32770";

        private static CUIAutomation8 _automation = new CUIAutomation8Class();

        private static IWebBrowser2 lastExplorerView = null;

        private static readonly InputSimulator _inputSimulator = new();

        private static UnhookWinEventSafeHandle _hookWinEventSafeHandle = null;

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

                lastExplorerView = explorer;
            });

            // Call WindowSwitch when the foreground window changes and check if there are explorer windows
            _hookWinEventSafeHandle = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    null,
                    WindowSwitch,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);

            if (_hookWinEventSafeHandle.IsInvalid)
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
                if (lastExplorerView != null)
                {
                    // Use dynamic here because using IWebBrower2.Document can cause exception here:
                    // System.Runtime.InteropServices.InvalidOleVariantTypeException: 'Specified OLE variant is invalid.'
                    dynamic explorerView = lastExplorerView;
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
                Win32Helper.DirJump(_inputSimulator, path, PInvoke.GetForegroundWindow());
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

        private static void WindowSwitch(
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

            if (_settings.AutoQuickSwitch)
            {
                if (window is { CurrentClassName: DialogWindowClassName })
                {
                    NavigateDialogPath();
                    return;
                }
            }

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

                    lastExplorerView = explorer;
                });
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to get shell windows", e);
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
            int count = shellWindows.Count;
            for (var i = 0; i < count; i++)
            {
                action(shellWindows.Item(i));
            }
        }

        public static void Dispose()
        {
            // Dispose handle
            if (_hookWinEventSafeHandle != null)
            {
                _hookWinEventSafeHandle.Dispose();
                _hookWinEventSafeHandle = null;
            }

            // Release ComObjects
            if (lastExplorerView != null)
            {
                Marshal.ReleaseComObject(lastExplorerView);
                lastExplorerView = null;
            }
            if (_automation != null)
            {
                Marshal.ReleaseComObject(_automation);
                _automation = null;
            }
        }
    }
}
