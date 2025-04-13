using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Interop.UIAutomationClient;
using NHotkey;
using SHDocVw;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Shell;
using WindowsInput;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        private static readonly string ClassName = nameof(QuickSwitch);

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        // The class name of a dialog window is "#32770".
        private const string DialogWindowClassName = "#32770";

        private static CUIAutomation8 _automation = new CUIAutomation8Class();

        private static InternetExplorer lastExplorerView = null;

        private static readonly InputSimulator _inputSimulator = new();

        private static UnhookWinEventSafeHandle _hookWinEventSafeHandle = null;

        public static bool Initialize()
        {
            try
            {
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
                    Log.Error("Failed to set window event hook");
                    return false;
                }

                return true;
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to initialize QuickSwitch", e);
            }

            return false;
        }

        public static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            NavigateDialogPath();
        }

        private static void NavigateDialogPath()
        {
            object document;
            try
            {
                document = lastExplorerView?.Document;
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

            ShellWindowsClass shellWindows;
            try
            {
                shellWindows = new ShellWindowsClass();

                foreach (var shellWindow in shellWindows)
                {
                    if (shellWindow is not InternetExplorer explorer)
                    {
                        continue;
                    }

                    if (explorer.HWND != hwnd.Value)
                    {
                        continue;
                    }

                    lastExplorerView = explorer;
                }
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to get shell windows", e);
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
