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
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        #region Public Properties

        public static Action<nint> ShowQuickSwitchWindow { get; set; } = null;

        public static Action UpdateQuickSwitchWindow { get; set; } = null;

        public static Action ResetQuickSwitchWindow { get; set; } = null;

        public static Action HideQuickSwitchWindow { get; set; } = null;

        public static QuickSwitchWindowPositions QuickSwitchWindowPosition { get; private set; }

        #endregion

        #region Private Fields

        // The class name of a dialog window
        private const string DialogWindowClassName = "#32770";

        private static readonly string ClassName = nameof(QuickSwitch);

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        private static IWebBrowser2 _lastExplorerView = null;
        private static readonly object _lastExplorerViewLock = new();

        private static HWND _mainWindowHandle = HWND.Null;

        private static HWND _dialogWindowHandle = HWND.Null;
        private static readonly object _dialogWindowHandleLock = new();

        private static HWINEVENTHOOK _foregroundChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _locationChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _destroyChangeHook = HWINEVENTHOOK.Null;

        private static DispatcherTimer _dragMoveTimer = null;

        // A list of all file dialog windows that are auto switched already
        private static readonly List<HWND> _autoSwitchedDialogs = new();
        private static readonly object _autoSwitchedDialogsLock = new();

        private static readonly SemaphoreSlim _navigationLock = new(1, 1);

        // Note: Here we do not start & stop the timer beacause when there are many dialog windows
        // Unhooking and hooking will take too much time which can make window position weird
        // So we start & stop the timer when we find a file dialog window
        /*private static HWINEVENTHOOK _moveSizeHook = HWINEVENTHOOK.Null;*/

        private static HWND _currentDialogWindowHandle = HWND.Null;
        private static readonly object _currentDialogWindowHandleLock = new();

        private static bool _initialized = false;
        private static bool _enabled = false;

        #endregion

        #region Initialize & Setup

        public static void InitializeQuickSwitch()
        {
            if (_initialized) return;

            // Initialize main window handle
            _mainWindowHandle = Win32Helper.GetMainWindowHandle();

            // Initialize timer
            _dragMoveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _dragMoveTimer.Tick += (s, e) => InvokeUpdateQuickSwitchWindow();

            // Initialize quick switch window position
            QuickSwitchWindowPosition = _settings.QuickSwitchWindowPosition;

            _initialized = true;
        }

        public static void SetupQuickSwitch(bool enabled)
        {
            if (enabled == _enabled) return;

            if (enabled)
            {
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

                            Log.Debug(ClassName, $"Explorer Window: {explorer.HWND.Value}");
                        }

                        // Force update explorer window if it is foreground
                        else if (Win32Helper.IsForegroundWindow(explorer.HWND.Value))
                        {
                            _lastExplorerView = explorer;

                            Log.Debug(ClassName, $"Explorer Window: {explorer.HWND.Value}");
                        }
                    });
                }

                // Unhook events
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
                if (!_destroyChangeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_destroyChangeHook);
                    _destroyChangeHook = HWINEVENTHOOK.Null;
                }

                // Hook events
                _foregroundChangeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    ForegroundChangeCallback,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
                _locationChangeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                    PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    LocationChangeCallback,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
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
                    _destroyChangeHook.IsNull)
                {
                    Log.Error(ClassName, "Failed to enable QuickSwitch");
                    return;
                }
            }
            else
            {
                // Remove last explorer
                lock (_lastExplorerViewLock)
                {
                    _lastExplorerView = null;
                }

                // Remove dialog window handle
                var dialogWindowExists = false;
                lock (_dialogWindowHandleLock)
                {
                    if (_dialogWindowHandle != HWND.Null)
                    {
                        _dialogWindowHandle = HWND.Null;
                        dialogWindowExists = true;
                    }
                }

                // Remove auto switched dialogs
                lock (_autoSwitchedDialogsLock)
                {
                    _autoSwitchedDialogs.Clear();
                }

                // Unhook events
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
                if (!_destroyChangeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_destroyChangeHook);
                    _destroyChangeHook = HWINEVENTHOOK.Null;
                }

                // Stop drag move timer
                _dragMoveTimer?.Stop();

                // Reset quick switch window
                if (dialogWindowExists)
                {
                    InvokeResetQuickSwitchWindow();
                }
            }

            _enabled = enabled;
        }

        #endregion

        #region Events

        #region Invoke Property Events

        private static unsafe void InvokeShowQuickSwitchWindow()
        {
            // Show quick switch window
            if (_settings.ShowQuickSwitchWindow)
            {
                lock (_currentDialogWindowHandleLock)
                {
                    var currentDialogWindowChanged = _currentDialogWindowHandle == HWND.Null ||
                        _currentDialogWindowHandle != _dialogWindowHandle;

                    if (currentDialogWindowChanged)
                    {
                        // Save quick switch window position for one file dialog
                        QuickSwitchWindowPosition = _settings.QuickSwitchWindowPosition;
                    }

                    _currentDialogWindowHandle = _dialogWindowHandle;
                }

                ShowQuickSwitchWindow?.Invoke(_dialogWindowHandle.Value);
                if (QuickSwitchWindowPosition == QuickSwitchWindowPositions.UnderDialog)
                {
                    _dragMoveTimer?.Start();
                }

                // Note: Here we do not start & stop the timer beacause when there are many dialog windows
                // Unhooking and hooking will take too much time which can make window position weird
                // So we start & stop the timer when we find a file dialog window
                /*lock (_currentDialogWindowHandleLock)
                {
                    var currentDialogWindowChanged = _currentDialogWindowHandle == HWND.Null ||
                        _currentDialogWindowHandle != _dialogWindowHandle;

                    if (currentDialogWindowChanged)
                    {
                        if (!_moveSizeHook.IsNull)
                        {
                            PInvoke.UnhookWinEvent(_moveSizeHook);
                            _moveSizeHook = HWINEVENTHOOK.Null;
                        }

                        // Call MoveSizeCallBack when the window is moved or resized
                        uint processId;
                        var threadId = PInvoke.GetWindowThreadProcessId(_dialogWindowHandle, &processId);
                        _moveSizeHook = PInvoke.SetWinEventHook(
                            PInvoke.EVENT_SYSTEM_MOVESIZESTART,
                            PInvoke.EVENT_SYSTEM_MOVESIZEEND,
                            PInvoke.GetModuleHandle((PCWSTR)null),
                            MoveSizeCallBack,
                            processId,
                            threadId,
                            PInvoke.WINEVENT_OUTOFCONTEXT);
                    }
                }*/
            }
        }

        private static void InvokeUpdateQuickSwitchWindow()
        {
            // Update quick switch window
            UpdateQuickSwitchWindow?.Invoke();
        }

        private static void InvokeResetQuickSwitchWindow()
        {
            // Reset quick switch window
            ResetQuickSwitchWindow?.Invoke();
            _dragMoveTimer?.Stop();

            lock (_currentDialogWindowHandleLock)
            {
                // Note: Here we do not start & stop the timer beacause when there are many dialog windows
                // Unhooking and hooking will take too much time which can make window position weird
                // So we start & stop the timer when we find a file dialog window
                /*if (!_moveSizeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_moveSizeHook);
                    _moveSizeHook = HWINEVENTHOOK.Null;
                }*/

                _currentDialogWindowHandle = HWND.Null;
            }
        }

        private static void InvokeHideQuickSwitchWindow()
        {
            // Neither quick switch window nor file dialog window is foreground
            // Hide quick switch window until the file dialog window is brought to the foreground
            HideQuickSwitchWindow?.Invoke();
            _dragMoveTimer?.Stop();
        }

        #endregion

        #region Hotkey

        public static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            NavigateDialogPath(Win32Helper.GetForegroundWindowHWND());
        }

        #endregion

        #region Windows Events

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
                Log.Debug(ClassName, $"Dialog Window: {hwnd}");

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
                        InvokeShowQuickSwitchWindow();
                    }
                    // Show quick switch window after navigating the path
                    else
                    {
                        NavigateDialogPath(hwnd, InvokeShowQuickSwitchWindow);
                    }
                }
                else
                {
                    InvokeShowQuickSwitchWindow();
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
                    InvokeHideQuickSwitchWindow();
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

                                Log.Debug(ClassName, $"Explorer Window: {hwnd}");
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
                InvokeUpdateQuickSwitchWindow();
            }
        }

        // Note: Here we do not start & stop the timer beacause when there are many dialog windows
        // Unhooking and hooking will take too much time which can make window position weird
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
            if (_dragMoveTimer != null)
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
                InvokeResetQuickSwitchWindow();
            }
        }

        #endregion

        #endregion

        #region Helper Methods

        #region Navigate Path

        // Edited from: https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump

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
                }
                ;

                // Assume that the dialog is in the foreground now
                await _navigationLock.WaitAsync();
                try
                {
                    var dialogHandle = new HWND(dialog);

                    bool result;
                    if (isFile)
                    {
                        switch (_settings.QuickSwitchFileResultBehaviour)
                        {
                            case QuickSwitchFileResultBehaviours.FullPath:
                                result = FileJump(path, dialogHandle, forceFileName: true);
                                Log.Debug(ClassName, $"File Jump FullPath: {path}");
                                break;
                            case QuickSwitchFileResultBehaviours.FullPathOpen:
                                result = FileJump(path, dialogHandle, forceFileName: true, openFile: true);
                                Log.Debug(ClassName, $"File Jump FullPathOpen: {path}");
                                break;
                            case QuickSwitchFileResultBehaviours.Directory:
                                result = DirJump(Path.GetDirectoryName(path), dialogHandle);
                                Log.Debug(ClassName, $"File Jump Directory: {path}");
                                break;
                            case QuickSwitchFileResultBehaviours.DirectoryAndFileName:
                                result = FileJump(path, dialogHandle);
                                Log.Debug(ClassName, $"File Jump DirectoryAndFileName: {path}");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(
                                    nameof(_settings.QuickSwitchFileResultBehaviour),
                                    _settings.QuickSwitchFileResultBehaviour,
                                    "Invalid QuickSwitchFileResultBehaviour"
                                );
                        }
                    }
                    else
                    {
                        result = DirJump(path, dialogHandle);
                        Log.Debug(ClassName, $"Dir Jump: {path}");
                    }

                    if (result)
                    {
                        lock (_autoSwitchedDialogsLock)
                        {
                            _autoSwitchedDialogs.Add(dialogHandle);
                        }
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

        private static bool FileJump(string filePath, HWND dialogHandle, bool forceFileName = false, bool openFile = false)
        {
            if (forceFileName)
            {
                return DirFileJumpForFileName(filePath, dialogHandle, openFile);
            }
            else
            {
                return DirFileJump(Path.GetDirectoryName(filePath), filePath, dialogHandle);
            }
        }

        private static bool DirJump(string dirPath, HWND dialogHandle)
        {
            return DirFileJump(dirPath, null, dialogHandle);
        }

        private static unsafe bool DirFileJump(string dirPath, string filePath, HWND dialogHandle)
        {
            // Get the handle of the path input box and then set the text.
            var controlHandle = PInvoke.GetDlgItem(dialogHandle, 0x0000); // WorkerW
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0xA005); // ReBarWindow32
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0xA205); // Address Band Root
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0x0000); // msctls_progress32
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0xA205); // ComboBoxEx32
            if (controlHandle == HWND.Null)
            {
                // https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump/issues/1
                // The dialog is a legacy one, so we edit file name text box directly.
                Log.Error(ClassName, "Failed to find control handle");
                return DirFileJumpForFileName(string.IsNullOrEmpty(filePath) ? dirPath : filePath, dialogHandle, true);
            }

            var timeOut = !SpinWait.SpinUntil(() =>
            {
                var style = PInvoke.GetWindowLong(controlHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                return (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
            }, 1000);
            if (timeOut)
            {
                Log.Error(ClassName, "Failed to find visible control handle");
                return false;
            }

            var editHandle = PInvoke.GetDlgItem(controlHandle, 0xA205); // ComboBox
            editHandle = PInvoke.GetDlgItem(editHandle, 0xA205); // Edit
            if (editHandle == HWND.Null)
            {
                Log.Error(ClassName, "Failed to find edit handle");
                return false;
            }

            SetWindowText(editHandle, dirPath);

            if (!string.IsNullOrEmpty(filePath))
            {
                // Note: I don't know why even openFile is set to false, the dialog still opens the file.
                return DirFileJumpForFileName(Path.GetFileName(filePath), dialogHandle, false);
            }

            return true;
        }

        /// <summary>
        /// Edit file name text box in the file open dialog.
        /// </summary>
        private static bool DirFileJumpForFileName(string fileName, HWND dialogHandle, bool openFile)
        {
            var controlHandle = PInvoke.GetDlgItem(dialogHandle, 0x047C); // ComboBoxEx32
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0x047C); // ComboBox
            controlHandle = PInvoke.GetDlgItem(controlHandle, 0x047C); // Edit
            if (controlHandle == HWND.Null)
            {
                Log.Error(ClassName, "Failed to find control handle");
                return false;
            }

            SetWindowText(controlHandle, fileName);

            if (openFile)
            {
                var openHandle = PInvoke.GetDlgItem(dialogHandle, 0x0001); // "&Open" Button
                if (openHandle == HWND.Null)
                {
                    Log.Error(ClassName, "Failed to find open handle");
                    return false;
                }

                ClickButton(openHandle);
            }

            return true;
        }

        private static unsafe nint SetWindowText(HWND handle, string text)
        {
            fixed (char* textPtr = text + '\0')
            {
                return PInvoke.SendMessage(handle, PInvoke.WM_SETTEXT, 0, (nint)textPtr).Value;
            }
        }

        private static unsafe nint ClickButton(HWND handle)
        {
            return PInvoke.PostMessage(handle, PInvoke.BM_CLICK, 0, 0).Value;
        }

        #endregion

        #region Class Name

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

        #endregion

        #region Enumerate Windows

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

        #endregion

        #endregion

        #region Dispose

        public static void Dispose()
        {
            // Reset flags
            _enabled = false;
            _initialized = false;

            // Unhook events
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
            // Note: Here we do not start & stop the timer beacause when there are many dialog windows
            // Unhooking and hooking will take too much time which can make window position weird
            // So we start & stop the timer when we find a file dialog window
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
            try
            {
                if (_lastExplorerView != null)
                {
                    Marshal.ReleaseComObject(_lastExplorerView);
                    _lastExplorerView = null;
                }
            }
            catch (COMException)
            {
                _lastExplorerView = null;
            }

            // Stop drag move timer
            if (_dragMoveTimer != null)
            {
                _dragMoveTimer.Stop();
                _dragMoveTimer = null;
            }
        }

        #endregion
    }
}
