using System;
using System.Threading;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.QuickSwitch.Interface;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Models
{
    internal class WindowsDialog : IQuickSwitchDialog
    {
        // The class name of a dialog window
        private const string WindowsDialogClassName = "#32770";

        public IQuickSwitchDialogWindow DialogWindow { get; private set; }

        public bool CheckDialogWindow(HWND hwnd)
        {
            if (GetWindowClassName(hwnd) == WindowsDialogClassName)
            {
                if (DialogWindow == null || DialogWindow.Handle != hwnd)
                {
                    DialogWindow = new WindowsDialogWindow(hwnd);
                }
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            DialogWindow?.Dispose();
            DialogWindow = null;
        }

        public static string GetWindowClassName(HWND handle)
        {
            return GetClassName(handle);

            static unsafe string GetClassName(HWND handle)
            {
                fixed (char* buf = new char[256])
                {
                    return PInvoke.GetClassName(handle, buf, 256) switch
                    {
                        0 => string.Empty,
                        _ => new string(buf),
                    };
                }
            }
        }
    }

    internal class WindowsDialogWindow : IQuickSwitchDialogWindow
    {
        public HWND Handle { get; private set; }

        public WindowsDialogWindow(HWND handle)
        {
            Handle = handle;
        }

        public IQuickSwitchDialogWindowTab GetCurrentTab()
        {
            return new WindowsDialogTab(Handle);
        }

        public void Dispose()
        {
            Handle = HWND.Null;
        }
    }

    internal class WindowsDialogTab : IQuickSwitchDialogWindowTab
    {
        public HWND Handle { get; private set; }

        private static readonly string ClassName = nameof(WindowsDialogTab);

        private readonly bool _legacy = false;

        private readonly HWND _pathControl;
        private readonly HWND _pathEditor;
        private readonly HWND _fileEditor;
        private readonly HWND _openButton;

        public WindowsDialogTab(HWND handle)
        {
            Handle = handle;

            // Get the handle of the path editor
            // The window with class name "ComboBoxEx32" is not visible when the path editor is not with the keyboard focus
            _pathControl = PInvoke.GetDlgItem(handle, 0x0000); // WorkerW
            _pathControl = PInvoke.GetDlgItem(_pathControl, 0xA005); // ReBarWindow32
            _pathControl = PInvoke.GetDlgItem(_pathControl, 0xA205); // Address Band Root
            _pathControl = PInvoke.GetDlgItem(_pathControl, 0x0000); // msctls_progress32
            _pathControl = PInvoke.GetDlgItem(_pathControl, 0xA205); // ComboBoxEx32
            if (_pathControl == HWND.Null)
            {
                // https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump/issues/1
                // The dialog is a legacy one, so we edit file name editor directly.
                _legacy = true;
                _pathEditor = HWND.Null;
                Log.Info(ClassName, "Failed to find path control handle - Legacy dialog");
            }
            else
            {
                _pathEditor = PInvoke.GetDlgItem(_pathControl, 0xA205); // ComboBox
                _pathEditor = PInvoke.GetDlgItem(_pathEditor, 0xA205); // Edit
                if (_pathEditor == HWND.Null)
                {
                    Log.Error(ClassName, "Failed to find path editor handle");
                }
            }

            // Get the handle of the file name editor of Open file dialog
            _fileEditor = PInvoke.GetDlgItem(handle, 0x047C); // ComboBoxEx32
            _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x047C); // ComboBox
            _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x047C); // Edit
            if (_fileEditor == HWND.Null)
            {
                // Get the handle of the file name editor of Save/SaveAs file dialog
                _fileEditor = PInvoke.GetDlgItem(handle, 0x0000); // DUIViewWndClassName
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x0000); // DirectUIHWND
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x0000); // FloatNotifySink
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x0000); // ComboBox
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x03E9); // Edit
                if (_fileEditor == HWND.Null)
                {
                    Log.Error(ClassName, "Failed to find file name editor handle");
                }
            }

            // Get the handle of the open button
            _openButton = PInvoke.GetDlgItem(handle, 0x0001); // Open/Save/SaveAs Button
            if (_openButton == HWND.Null)
            {
                Log.Error(ClassName, "Failed to find open button handle");
            }
        }

        public string GetCurrentFolder()
        {
            if (_pathEditor.IsNull) return string.Empty;
            return GetWindowText(_pathEditor);
        }

        public string GetCurrentFile()
        {
            if (_fileEditor.IsNull) return string.Empty;
            return GetWindowText(_fileEditor);
        }

        public bool JumpFolder(string path, bool auto)
        {
            if (_legacy || auto)
            {
                // https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump/issues/1
                // The dialog is a legacy one, so we edit file name text box directly
                if (_fileEditor.IsNull) return false;
                SetWindowText(_fileEditor, path);

                if (_openButton.IsNull) return false;
                PInvoke.SendMessage(_openButton, PInvoke.BM_CLICK, 0, 0);

                return true;
            }

            if (_pathControl.IsNull) return false;

            var timeOut = !SpinWait.SpinUntil(() =>
            {
                var style = PInvoke.GetWindowLong(_pathControl, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                return (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
            }, 1000);
            if (timeOut)
            {
                Log.Error(ClassName, "Path control is not visible");
                return false;
            }

            if (_pathEditor.IsNull) return false;

            SetWindowText(_pathEditor, path);
            return true;
        }

        public bool JumpFile(string path)
        {
            if (_fileEditor.IsNull) return false;
            SetWindowText(_fileEditor, path);
            return true;
        }

        public bool Open()
        {
            if (_openButton.IsNull) return false;
            PInvoke.PostMessage(_openButton, PInvoke.BM_CLICK, 0, 0);
            return true;
        }

        private static unsafe string GetWindowText(HWND handle)
        {
            int length;
            Span<char> buffer = stackalloc char[1000];
            fixed (char* pBuffer = buffer)
            {
                // If the control has no title bar or text, or if the control handle is invalid, the return value is zero.
                length = (int)PInvoke.SendMessage(handle, PInvoke.WM_GETTEXT, 1000, (nint)pBuffer);
            }

            return buffer[..length].ToString();
        }

        private static unsafe nint SetWindowText(HWND handle, string text)
        {
            fixed (char* textPtr = text + '\0')
            {
                return PInvoke.SendMessage(handle, PInvoke.WM_SETTEXT, 0, (nint)textPtr).Value;
            }
        }

        public void Dispose()
        {
            Handle = HWND.Null;
        }
    }
}
