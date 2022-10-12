using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using Interop.UIAutomationClient;
using SHDocVw;
using Shell32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Native;
using static Flow.Launcher.QuickSwitch.NativeHelper;

namespace Flow.Launcher.QuickSwitch
{
    public static class QuickSwitch
    {
        private static CUIAutomation8 _automation = new CUIAutomation8Class();

        private static InternetExplorer lastExplorerView;

        private static InputSimulator _inputSimulator = new();

        private static IntPtr hookId;

        public static unsafe void Initialize()
        {
            hookId = SetWinEventHook(EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                &WindowSwitch,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            HotKeyMapper.SetHotkey(new HotkeyModel("Alt+G"), (_, _) =>
            {
                NavigateDialogPath(_automation.ElementFromHandle(GetForegroundWindow()));
            });
        }

        private static void NavigateDialogPath(IUIAutomationElement window)
        {
            if (window is not { CurrentClassName: "#32770" } dialog)
            {
                return;
            }
            object? document;
            try
            {
                document = lastExplorerView?.Document;
            }
            catch (COMException e)
            {
                return;
            }
            if (document is not IShellFolderViewDual2 folder)
                return;

            var path = folder.Folder.Items().Item().Path;
            if (!Path.IsPathRooted(path))
                return;


            _inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.MENU, VirtualKeyCode.VK_D);

            var address = dialog.FindFirst(TreeScope.TreeScope_Subtree, _automation.CreateAndCondition(
                _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId),
                _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AccessKeyPropertyId, "d")));

            if (address == null)
            {
                Log.Error("Cannot Get specific Control");
                return;
            }

            var edit = (IUIAutomationValuePattern)address.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId);
            edit.SetValue(path);

            SendMessage(address.CurrentNativeWindowHandle, NativeHelper.WmType.WM_KEYDOWN, (nuint)VirtualKeyCode.RETURN, IntPtr.Zero);


        }

        [UnmanagedCallersOnly]
        private static void WindowSwitch(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
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

            ShellWindows shellWindows = new ShellWindowsClass();

            foreach (var shellWindow in shellWindows)
            {
                if (shellWindow is not InternetExplorer explorer)
                {
                    continue;
                }

                if (explorer.HWND != (int)hwnd)
                {
                    continue;
                }
                lastExplorerView = explorer;

            }
        }
    }
}