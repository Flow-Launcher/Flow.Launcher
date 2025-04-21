using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.QuickSwitch.Interface;
using Flow.Launcher.Infrastructure.QuickSwitch.Models;
using Flow.Launcher.Infrastructure.UserSettings;
using NHotkey;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

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

        private static readonly string ClassName = nameof(QuickSwitch);

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        private static HWND _mainWindowHandle = HWND.Null;

        private static readonly List<IQuickSwitchExplorer> _quickSwitchExplorers = new()
        {
            new WindowsExplorer()
        };

        private static IQuickSwitchExplorer _lastExplorer = null;
        private static readonly object _lastExplorerLock = new();

        private static readonly List<IQuickSwitchDialog> _quickSwitchDialogs = new()
        {
            new WindowsDialog()
        };

        private static IQuickSwitchDialogWindow _dialogWindow = null;
        private static readonly object _dialogWindowLock = new();

        private static HWINEVENTHOOK _foregroundChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _locationChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _destroyChangeHook = HWINEVENTHOOK.Null;

        private static readonly WINEVENTPROC _fgProc = ForegroundChangeCallback;
        private static readonly WINEVENTPROC _locProc = LocationChangeCallback;
        private static readonly WINEVENTPROC _desProc = DestroyChangeCallback;

        private static DispatcherTimer _dragMoveTimer = null;

        // A list of all file dialog windows that are auto switched already
        private static readonly List<HWND> _autoSwitchedDialogs = new();
        private static readonly object _autoSwitchedDialogsLock = new();

        // Note: Here we do not start & stop the timer beacause when there are many dialog windows
        // Unhooking and hooking will take too much time which can make window position weird
        // So we start & stop the timer when we find a file dialog window
        /*private static HWINEVENTHOOK _moveSizeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _moveProc = MoveSizeCallBack;*/

        private static readonly SemaphoreSlim _navigationLock = new(1, 1);

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
                // Check if there are explorer windows
                try
                {
                    lock (_lastExplorerLock)
                    {
                        foreach (var explorer in _quickSwitchExplorers)
                        {
                            // Use HWND.Null here because we want to check all windows
                            if (explorer.CheckExplorerWindow(HWND.Null))
                            {
                                if (_lastExplorer == null)
                                {
                                    Log.Debug(ClassName, $"Explorer window");
                                    // Set last explorer view if not set,
                                    // this is beacuse default WindowsExplorer is the first element
                                    _lastExplorer = explorer;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Ignored
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
                    _fgProc,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
                _locationChangeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                    PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    _locProc,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
                _destroyChangeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_OBJECT_DESTROY,
                    PInvoke.EVENT_OBJECT_DESTROY,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    _desProc,
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
                foreach (var explorer in _quickSwitchExplorers)
                {
                    explorer.RemoveExplorerWindow();
                }

                // Remove dialog window handle
                var dialogWindowExists = false;
                lock (_dialogWindowLock)
                {
                    if (_dialogWindow != null)
                    {
                        _dialogWindow.Dispose();
                        _dialogWindow = null;
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

        private static unsafe void InvokeShowQuickSwitchWindow(bool dialogWindowChanged)
        {
            // Show quick switch window
            if (_settings.ShowQuickSwitchWindow)
            {
                if (dialogWindowChanged)
                {
                    // Save quick switch window position for one file dialog
                    QuickSwitchWindowPosition = _settings.QuickSwitchWindowPosition;
                }

                ShowQuickSwitchWindow?.Invoke(_dialogWindow.Handle);
                if (QuickSwitchWindowPosition == QuickSwitchWindowPositions.UnderDialog)
                {
                    _dragMoveTimer?.Start();
                    // Note: Here we do not start & stop the timer beacause when there are many dialog windows
                    // Unhooking and hooking will take too much time which can make window position weird
                    // So we start & stop the timer when we find a file dialog window
                    /*if (dialogWindowChanged)
                    {
                        if (!_moveSizeHook.IsNull)
                        {
                            PInvoke.UnhookWinEvent(_moveSizeHook);
                            _moveSizeHook = HWINEVENTHOOK.Null;
                        }

                        // Call _moveProc when the window is moved or resized
                        uint processId;
                        var threadId = PInvoke.GetWindowThreadProcessId(_dialogWindow.Handle, &processId);
                        _moveSizeHook = PInvoke.SetWinEventHook(
                            PInvoke.EVENT_SYSTEM_MOVESIZESTART,
                            PInvoke.EVENT_SYSTEM_MOVESIZEEND,
                            PInvoke.GetModuleHandle((PCWSTR)null),
                            _moveProc,
                            processId,
                            threadId,
                            PInvoke.WINEVENT_OUTOFCONTEXT);
                    }*/
                }
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
            // Note: Here we do not start & stop the timer beacause when there are many dialog windows
            // Unhooking and hooking will take too much time which can make window position weird
            // So we start & stop the timer when we find a file dialog window
            /*if (!_moveSizeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_moveSizeHook);
                _moveSizeHook = HWINEVENTHOOK.Null;
            }*/

            lock (_dialogWindowLock)
            {
                _dialogWindow = null;
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
            var findDialogWindow = false;
            var dialogWindowChanged = false;
            foreach (var dialog in _quickSwitchDialogs)
            {
                if (dialog.CheckDialogWindow(hwnd))
                {
                    lock (_dialogWindowLock)
                    {
                        dialogWindowChanged = _dialogWindow == null || _dialogWindow.Handle != hwnd;
                        _dialogWindow = dialog.DialogWindow;
                    }

                    findDialogWindow = true;
                    Log.Debug(ClassName, $"Dialog Window: {hwnd}");
                    break;
                }
            }
            if (findDialogWindow)
            {
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
                        InvokeShowQuickSwitchWindow(dialogWindowChanged);
                    }
                    // Show quick switch window after navigating the path
                    else
                    {
                        if (!NavigateDialogPath(hwnd, true, () => InvokeShowQuickSwitchWindow(dialogWindowChanged)))
                        {
                            InvokeShowQuickSwitchWindow(dialogWindowChanged);
                        }
                    }
                }
                else
                {
                    InvokeShowQuickSwitchWindow(dialogWindowChanged);
                }
            }
            // Quick switch window
            else if (hwnd == _mainWindowHandle)
            {
                // Nothing to do
            }
            else
            {
                if (_dialogWindow != null)
                {
                    InvokeHideQuickSwitchWindow();
                }

                // Check if there are foreground explorer windows
                try
                {
                    lock (_lastExplorerLock)
                    {
                        foreach (var explorer in _quickSwitchExplorers)
                        {
                            if (explorer.CheckExplorerWindow(hwnd))
                            {
                                Log.Debug(ClassName, $"Explorer window: {hwnd}");
                                _lastExplorer = explorer;
                                break;
                            }
                        }
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
            if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
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
            if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
            {
                Log.Debug(ClassName, $"Destory dialog: {hwnd}");
                lock (_dialogWindowLock)
                {
                    _dialogWindow = null;
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

        #region Path Navigation

        // Edited from: https://github.com/idkidknow/Flow.Launcher.Plugin.DirQuickJump

        public static void JumpToPath(nint hwnd, string path, Action action = null)
        {
            if (hwnd == nint.Zero) return;

            var dialogWindow = GetDialogWindow(new(hwnd));
            if (dialogWindow == null) return;

            var dialogWindowTab = dialogWindow.GetCurrentTab();
            if (dialogWindowTab == null) return;

            JumpToPath(dialogWindowTab, path, false, action);
        }

        private static bool NavigateDialogPath(HWND hwnd, bool auto = false, Action action = null)
        {
            if (hwnd == HWND.Null) return false;

            var dialogWindow = GetDialogWindow(hwnd);
            if (dialogWindow == null) return false;

            var dialogWindowTab = dialogWindow.GetCurrentTab();
            if (dialogWindowTab == null) return false;

            // Get explorer path
            string path;
            lock (_lastExplorerLock)
            {
                path = _lastExplorer?.GetExplorerPath();
            }
            if (string.IsNullOrEmpty(path)) return false;

            // Jump to path
            return JumpToPath(dialogWindowTab, path, auto, action);
        }

        private static IQuickSwitchDialogWindow GetDialogWindow(HWND hwnd)
        {
            // First check dialog window
            lock (_dialogWindowLock)
            {
                if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
                {
                    return _dialogWindow;
                }
            }

            // Then check all dialog windows
            foreach (var dialog in _quickSwitchDialogs)
            {
                if (dialog.DialogWindow.Handle == hwnd)
                {
                    return dialog.DialogWindow;
                }
            }

            // Finally search for the dialog window
            foreach (var dialog in _quickSwitchDialogs)
            {
                if (dialog.CheckDialogWindow(hwnd))
                {
                    return dialog.DialogWindow;
                }
            }

            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "<Pending>")]
        private static bool JumpToPath(IQuickSwitchDialogWindowTab dialog, string path, bool auto = false, Action action = null)
        {
            if (!CheckPath(path, out var isFile)) return false;

            var t = new Thread(async () =>
            {
                // Jump after flow launcher window vanished (after JumpAction returned true)
                // and the dialog had been in the foreground.
                var dialogHandle = dialog.Handle;
                var timeOut = !SpinWait.SpinUntil(() => Win32Helper.GetForegroundWindowHWND() == dialogHandle, 1000);
                if (timeOut)
                {
                    action?.Invoke();
                    return;
                }

                // Assume that the dialog is in the foreground now
                await _navigationLock.WaitAsync();
                try
                {
                    bool result;
                    if (isFile)
                    {
                        switch (_settings.QuickSwitchFileResultBehaviour)
                        {
                            case QuickSwitchFileResultBehaviours.FullPath:
                                Log.Debug(ClassName, $"File Jump FullPath: {path}");
                                result = FileJump(path, dialog);
                                break;
                            case QuickSwitchFileResultBehaviours.FullPathOpen:
                                Log.Debug(ClassName, $"File Jump FullPathOpen: {path}");
                                result = FileJump(path, dialog, openFile: true);
                                break;
                            case QuickSwitchFileResultBehaviours.Directory:
                                Log.Debug(ClassName, $"File Jump Directory (Auto: {auto}): {path}");
                                result = DirJump(Path.GetDirectoryName(path), dialog, auto);
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
                        Log.Debug(ClassName, $"Dir Jump: {path}");
                        result = DirJump(path, dialog, auto);
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
            return true;

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

        private static bool FileJump(string filePath, IQuickSwitchDialogWindowTab dialog, bool openFile = false)
        {
            if (!dialog.JumpFile(filePath))
            {
                Log.Error(ClassName, "Failed to jump file");
                return false;
            }

            if (openFile && !dialog.Open())
            {
                Log.Error(ClassName, "Failed to open file");
                return false;
            }

            return true;
        }

        private static bool DirJump(string dirPath, IQuickSwitchDialogWindowTab dialog, bool auto = false)
        {
            if (!dialog.JumpFolder(dirPath, auto))
            {
                Log.Error(ClassName, "Failed to jump folder");
                return false;
            }

            return true;
        }

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

            // Dispose explorers
            foreach (var explorer in _quickSwitchExplorers)
            {
                explorer.Dispose();
            }
            _quickSwitchExplorers.Clear();
            lock (_lastExplorerLock)
            {
                _lastExplorer = null;
            }

            // Dispose dialogs
            foreach (var dialog in _quickSwitchDialogs)
            {
                dialog.Dispose();
            }
            _quickSwitchDialogs.Clear();
            lock (_dialogWindowLock)
            {
                _dialogWindow = null;
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
