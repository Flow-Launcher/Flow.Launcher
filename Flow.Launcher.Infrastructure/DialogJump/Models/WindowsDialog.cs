using System;
using System.Threading;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowsInput;
using WindowsInput.Native;

namespace Flow.Launcher.Infrastructure.DialogJump.Models
{
    /// <summary>
    /// Class for handling Windows File Dialog instances in DialogJump.
    /// </summary>
    public class WindowsDialog : IDialogJumpDialog
    {
        private const string WindowsDialogClassName = "#32770";

        public IDialogJumpDialogWindow CheckDialogWindow(IntPtr hwnd)
        {
            // Is it a Win32 dialog box?
            if (GetClassName(new(hwnd)) == WindowsDialogClassName)
            {
                // Is it a windows file dialog?
                var dialogType = GetFileDialogType(new(hwnd));
                if (dialogType != DialogType.Others)
                {
                    return new WindowsDialogWindow(hwnd, dialogType);
                }
            }

            return null;
        }

        public void Dispose()
        {

        }

        #region Help Methods

        private static unsafe string GetClassName(HWND handle)
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

        private static DialogType GetFileDialogType(HWND handle)
        {
            // Is it a Windows Open file dialog?
            var fileEditor = PInvoke.GetDlgItem(handle, 0x047C);
            if (fileEditor != HWND.Null && GetClassName(fileEditor) == "ComboBoxEx32") return DialogType.Open;

            // Is it a Windows Save or Save As file dialog?
            fileEditor = PInvoke.GetDlgItem(handle, 0x0000);
            if (fileEditor != HWND.Null && GetClassName(fileEditor) == "DUIViewWndClassName") return DialogType.SaveOrSaveAs;

            return DialogType.Others;
        }

        #endregion
    }

    public class WindowsDialogWindow : IDialogJumpDialogWindow
    {
        public IntPtr Handle { get; private set; } = IntPtr.Zero;

        // After jumping folder, file editor handle of Save / SaveAs file dialogs cannot be found anymore
        // So we need to cache the current tab and use the original handle
        private IDialogJumpDialogWindowTab _currentTab { get; set; } = null;

        private readonly DialogType _dialogType;

        internal WindowsDialogWindow(IntPtr handle, DialogType dialogType)
        {
            Handle = handle;
            _dialogType = dialogType;
        }

        public IDialogJumpDialogWindowTab GetCurrentTab()
        {
            return _currentTab ??= new WindowsDialogTab(Handle, _dialogType);
        }

        public void Dispose()
        {

        }
    }

    public class WindowsDialogTab : IDialogJumpDialogWindowTab
    {
        #region Public Properties

        public IntPtr Handle { get; private set; } = IntPtr.Zero;

        #endregion

        #region Private Fields

        private static readonly string ClassName = nameof(WindowsDialogTab);

        private static readonly InputSimulator _inputSimulator = new();

        private readonly DialogType _dialogType;

        private bool _legacy { get; set; } = false;
        private HWND _pathControl { get; set; } = HWND.Null;
        private HWND _pathEditor { get; set; } = HWND.Null;
        private HWND _fileEditor { get; set; } = HWND.Null;
        private HWND _openButton { get; set; } = HWND.Null;

        #endregion

        #region Constructor

        internal WindowsDialogTab(IntPtr handle, DialogType dialogType)
        {
            Handle = handle;
            _dialogType = dialogType;
            Log.Debug(ClassName, $"File dialog type: {dialogType}");
        }

        #endregion

        #region Public Methods

        public string GetCurrentFolder()
        {
            if (_pathEditor.IsNull && !GetPathControlEditor()) return string.Empty;
            return GetWindowText(_pathEditor);
        }

        public string GetCurrentFile()
        {
            if (_fileEditor.IsNull && !GetFileEditor()) return string.Empty;
            return GetWindowText(_fileEditor);
        }

        public bool JumpFolder(string path, bool auto)
        {
            if (auto)
            {
                // Use legacy jump folder method for auto dialog jump because file editor is default value.
                // After setting path using file editor, we do not need to revert its value.
                return JumpFolderWithFileEditor(path, false);
            }

            // Alt-D or Ctrl-L to focus on the path input box
            // "ComboBoxEx32" is not visible when the path editor is not with the keyboard focus
            _inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LMENU, VirtualKeyCode.VK_D);
            // _inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_L);

            if (_pathControl.IsNull && !GetPathControlEditor())
            {
                // https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump/issues/1
                // The dialog is a legacy one, so we can only edit file editor directly.
                Log.Debug(ClassName, "Legacy dialog, using legacy jump folder method");
                return JumpFolderWithFileEditor(path, true);
            }

            var timeOut = !SpinWait.SpinUntil(() =>
            {
                var style = PInvoke.GetWindowLongPtr(_pathControl, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                return (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
            }, 1000);
            if (timeOut)
            {
                // Path control is not visible, so we can only edit file editor directly.
                Log.Debug(ClassName, "Path control is not visible, using legacy jump folder method");
                return JumpFolderWithFileEditor(path, true);
            }

            if (_pathEditor.IsNull)
            {
                // Path editor cannot be found, so we can only edit file editor directly.
                Log.Debug(ClassName, "Path editor cannot be found, using legacy jump folder method");
                return JumpFolderWithFileEditor(path, true);
            }
            SetWindowText(_pathEditor, path);

            _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);

            return true;
        }

        public bool JumpFile(string path)
        {
            if (_fileEditor.IsNull && !GetFileEditor()) return false;
            SetWindowText(_fileEditor, path);

            return true;
        }

        public bool Open()
        {
            if (_openButton.IsNull && !GetOpenButton()) return false;
            PInvoke.PostMessage(_openButton, PInvoke.BM_CLICK, 0, 0);

            return true;
        }

        public void Dispose()
        {
            
        }

        #endregion

        #region Helper Methods

        #region Get Handles

        private bool GetPathControlEditor()
        {
            // Get the handle of the path editor
            // Must use PInvoke.FindWindowEx because PInvoke.GetDlgItem(Handle, 0x0000) will get another control
            _pathControl = PInvoke.FindWindowEx(new(Handle), HWND.Null, "WorkerW", null); // 0x0000
            _pathControl = PInvoke.FindWindowEx(_pathControl, HWND.Null, "ReBarWindow32", null); // 0xA005
            _pathControl = PInvoke.FindWindowEx(_pathControl, HWND.Null, "Address Band Root", null); // 0xA205
            _pathControl = PInvoke.FindWindowEx(_pathControl, HWND.Null, "msctls_progress32", null); // 0x0000
            _pathControl = PInvoke.FindWindowEx(_pathControl, HWND.Null, "ComboBoxEx32", null); // 0xA205
            if (_pathControl == HWND.Null)
            {
                _pathEditor = HWND.Null;
                _legacy = true;
                Log.Info(ClassName, "Legacy dialog");
            }
            else
            {
                _pathEditor = PInvoke.GetDlgItem(_pathControl, 0xA205); // ComboBox
                _pathEditor = PInvoke.GetDlgItem(_pathEditor, 0xA205); // Edit
                if (_pathEditor == HWND.Null)
                {
                    _legacy = true;
                    Log.Error(ClassName, "Failed to find path editor handle");
                }
            }

            return !_legacy;
        }

        private bool GetFileEditor()
        {
            if (_dialogType == DialogType.Open)
            {
                // Get the handle of the file name editor of Open file dialog
                _fileEditor = PInvoke.GetDlgItem(new(Handle), 0x047C); // ComboBoxEx32
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x047C); // ComboBox
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x047C); // Edit
            }
            else
            {
                // Get the handle of the file name editor of Save / SaveAs file dialog
                _fileEditor = PInvoke.GetDlgItem(new(Handle), 0x0000); // DUIViewWndClassName
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x0000); // DirectUIHWND
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x0000); // FloatNotifySink
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x0000); // ComboBox
                _fileEditor = PInvoke.GetDlgItem(_fileEditor, 0x03E9); // Edit
            }

            if (_fileEditor == HWND.Null)
            {
                Log.Error(ClassName, "Failed to find file name editor handle");
                return false;
            }

            return true;
        }

        private bool GetOpenButton()
        {
            // Get the handle of the open button
            _openButton = PInvoke.GetDlgItem(new(Handle), 0x0001); // Open/Save/SaveAs Button
            if (_openButton == HWND.Null)
            {
                Log.Error(ClassName, "Failed to find open button handle");
                return false;
            }

            return true;
        }

        #endregion

        #region Windows Text

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

        #endregion

        #region Legacy Jump Folder

        private bool JumpFolderWithFileEditor(string path, bool resetFocus)
        {
            // For Save / Save As dialog, the default value in file editor is not null and it can cause strange behaviors.
            if (resetFocus && _dialogType == DialogType.SaveOrSaveAs) return false;

            if (_fileEditor.IsNull && !GetFileEditor()) return false;
            SetWindowText(_fileEditor, path);

            if (_openButton.IsNull && !GetOpenButton()) return false;
            PInvoke.SendMessage(_openButton, PInvoke.BM_CLICK, 0, 0);

            return true;
        }

        #endregion

        #endregion
    }

    internal enum DialogType
    {
        Others,
        Open,
        SaveOrSaveAs
    }
}
