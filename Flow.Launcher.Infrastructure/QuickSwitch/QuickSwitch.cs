using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Flow.Launcher.Infrastructure.Logger;
using Interop.UIAutomationClient;
using NHotkey;
using SHDocVw;
using Shell32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using WindowsInput;
using WindowsInput.Native;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        private static readonly string ClassName = nameof(QuickSwitch);

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
            NavigateDialogPath(_automation.ElementFromHandle(Win32Helper.GetForegroundWindow()));
        }

        private static void NavigateDialogPath(IUIAutomationElement window)
        {
            if (window is not { CurrentClassName: "#32770" } dialog)
            {
                return;
            }

            object document;
            try
            {
                document = lastExplorerView?.Document;
            }
            catch (COMException)
            {
                return;
            }

            if (document is not IShellFolderViewDual2 folder)
            {
                return;
            }

            string path;
            try
            {
                path = folder.Folder.Items().Item().Path;
                if (!Path.IsPathRooted(path))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            //_inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.MENU, VirtualKeyCode.VK_D);

            var address = dialog.FindFirst(TreeScope.TreeScope_Subtree, _automation.CreateAndCondition(
                _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId),
                _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AccessKeyPropertyId, "d")));

            if (address == null)
            {
                // I found issue here
                Debug.WriteLine("Failed to find address edit control");
                return;
            }

            var edit = (IUIAutomationValuePattern)address.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId);
            edit.SetValue(path);

            PInvoke.SendMessage(
                new(address.CurrentNativeWindowHandle),
                PInvoke.WM_KEYDOWN,
                (nuint)VirtualKeyCode.RETURN,
                IntPtr.Zero);
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

            if (window is { CurrentClassName: "#32770" })
            {
                NavigateDialogPath(window);
                return;
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

                    // Fix for CA2020: Wrap the conversion in a 'checked' statement
                    if (explorer.HWND != checked((int)hwnd))
                    {
                        continue;
                    }

                    // Release previous reference if exists
                    /*if (lastExplorerView != null)
                    {
                        Marshal.ReleaseComObject(lastExplorerView);
                        lastExplorerView = null;
                    }*/

                    lastExplorerView = explorer;
                }
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to get shell windows", e);
            }
            finally
            {
                /*if (window != null)
                {
                    Marshal.ReleaseComObject(window);
                    window = null;
                }
                if (shellWindows != null)
                {
                    Marshal.ReleaseComObject(shellWindows);
                    shellWindows = null;
                }*/
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
