using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.QuickSwitch.Models;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugins;
using NHotkey;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Flow.Launcher.Infrastructure.QuickSwitch
{
    public static class QuickSwitch
    {
        #region Public Properties

        public static Func<nint, Task> ShowQuickSwitchWindowAsync { get; set; } = null;

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

        private static HWINEVENTHOOK _moveSizeHook = HWINEVENTHOOK.Null;
        private static readonly WINEVENTPROC _moveProc = MoveSizeCallBack;

        private static readonly SemaphoreSlim _foregroundChangeLock = new(1, 1);
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
                // Check if there are explorer windows and get the topmost one
                try
                {
                    if (RefreshLastExplorer())
                    {
                        Log.Debug(ClassName, $"Explorer window found");
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

        private static bool RefreshLastExplorer()
        {
            var found = false;

            lock (_lastExplorerLock)
            {
                // Enum windows from the top to the bottom
                PInvoke.EnumWindows((hWnd, _) =>
                {
                    foreach (var explorer in _quickSwitchExplorers)
                    {
                        if (explorer.CheckExplorerWindow(hWnd))
                        {
                            _lastExplorer = explorer;
                            found = true;
                            return false;
                        }
                    }

                    // If we reach here, it means that the window is not a file explorer
                    return true;
                }, IntPtr.Zero);
            }

            return found;
        }

        #endregion

        #region Active Explorer

        public static string GetActiveExplorerPath()
        {
            return RefreshLastExplorer() ? _lastExplorer.ExplorerWindow?.GetExplorerPath() : string.Empty;
        }

        #endregion

        #region Events

        #region Invoke Property Events

        private static async Task InvokeShowQuickSwitchWindowAsync(bool dialogWindowChanged)
        {
            // Show quick switch window
            if (_settings.ShowQuickSwitchWindow)
            {
                // Save quick switch window position for one file dialog
                if (dialogWindowChanged)
                {
                    QuickSwitchWindowPosition = _settings.QuickSwitchWindowPosition;
                }

                // Call show quick switch window
                IQuickSwitchDialogWindow dialogWindow;
                lock (_dialogWindowLock)
                {
                    dialogWindow = _dialogWindow;
                }
                if (dialogWindow != null && ShowQuickSwitchWindowAsync != null)
                {
                    await ShowQuickSwitchWindowAsync.Invoke(dialogWindow.Handle);
                }

                // Hook move size event if quick switch window is under dialog & dialog window changed
                if (QuickSwitchWindowPosition == QuickSwitchWindowPositions.UnderDialog)
                {
                    if (dialogWindowChanged)
                    {
                        HWND dialogWindowHandle = HWND.Null;
                        lock (_dialogWindowLock)
                        {
                            if (_dialogWindow != null)
                            {
                                dialogWindowHandle = new(_dialogWindow.Handle);
                            }
                        }

                        if (dialogWindowHandle == HWND.Null) return;

                        if (!_moveSizeHook.IsNull)
                        {
                            PInvoke.UnhookWinEvent(_moveSizeHook);
                            _moveSizeHook = HWINEVENTHOOK.Null;
                        }

                        // Call _moveProc when the window is moved or resized
                        SetMoveProc(dialogWindowHandle);
                    }
                }
            }

            static unsafe void SetMoveProc(HWND handle)
            {
                uint processId;
                var threadId = PInvoke.GetWindowThreadProcessId(handle, &processId);
                _moveSizeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_SYSTEM_MOVESIZESTART,
                    PInvoke.EVENT_SYSTEM_MOVESIZEEND,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    _moveProc,
                    processId,
                    threadId,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
            }
        }

        private static void InvokeUpdateQuickSwitchWindow()
        {
            UpdateQuickSwitchWindow?.Invoke();
        }

        private static void InvokeResetQuickSwitchWindow()
        {
            lock (_dialogWindowLock)
            {
                _dialogWindow = null;
            }

            // Reset quick switch window
            ResetQuickSwitchWindow?.Invoke();

            // Stop drag move timer
            _dragMoveTimer?.Stop();

            // Unhook move size event
            if (!_moveSizeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_moveSizeHook);
                _moveSizeHook = HWINEVENTHOOK.Null;
            }
        }

        private static void InvokeHideQuickSwitchWindow()
        {
            // Hide quick switch window
            HideQuickSwitchWindow?.Invoke();

            // Stop drag move timer
            _dragMoveTimer?.Stop();
        }

        #endregion

        #region Hotkey

        public static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            _ = Task.Run(() => NavigateDialogPathAsync(PInvoke.GetForegroundWindow()));
        }

        #endregion

        #region Windows Events

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private static async void ForegroundChangeCallback(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            await _foregroundChangeLock.WaitAsync();
            try
            {
                // Check if it is a file dialog window
                var isDialogWindow = false;
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

                        isDialogWindow = true;
                        break;
                    }
                }

                // Handle window based on its type
                if (isDialogWindow)
                {
                    Log.Debug(ClassName, $"Dialog Window: {hwnd}");
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
                            await InvokeShowQuickSwitchWindowAsync(dialogWindowChanged);
                        }
                        // Show quick switch window after navigating the path
                        else
                        {
                            if (!await Task.Run(() => NavigateDialogPathAsync(hwnd, true)))
                            {
                                await InvokeShowQuickSwitchWindowAsync(dialogWindowChanged);
                            }
                        }
                    }
                    else
                    {
                        await InvokeShowQuickSwitchWindowAsync(dialogWindowChanged);
                    }
                }
                // Quick switch window
                else if (hwnd == _mainWindowHandle)
                {
                    Log.Debug(ClassName, $"Main Window: {hwnd}");
                }
                // Other window
                else
                {
                    Log.Debug(ClassName, $"Other Window: {hwnd}");
                    var dialogWindowExist = false;
                    lock (_dialogWindowLock)
                    {
                        if (_dialogWindow != null)
                        {
                            dialogWindowExist = true;
                        }
                    }
                    if (dialogWindowExist) // Neither quick switch window nor file dialog window is foreground
                    {
                        // Hide quick switch window until the file dialog window is brought to the foreground
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
            finally
            {
                _foregroundChangeLock.Release();
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
            var dialogWindowExist = false;
            lock (_dialogWindowLock)
            {
                if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
                {
                    dialogWindowExist = true;
                }
            }
            if (dialogWindowExist)
            {
                InvokeUpdateQuickSwitchWindow();
            }
        }

        private static void MoveSizeCallBack(
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
            // If the dialog window is destroyed, set _dialogWindowHandle to null
            var dialogWindowExist = false;
            lock (_dialogWindowLock)
            {
                if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
                {
                    Log.Debug(ClassName, $"Destory dialog: {hwnd}");
                    _dialogWindow = null;
                    dialogWindowExist = true;
                }
            }
            if (dialogWindowExist)
            {
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

        public static async Task<bool> JumpToPathAsync(nint hwnd, string path)
        {
            // Check handle
            if (hwnd == nint.Zero) return false;

            // Check path
            if (!CheckPath(path, out var isFile)) return false;

            // Get dialog tab
            var dialogWindowTab = GetDialogWindowTab(new(hwnd));
            if (dialogWindowTab == null) return false;

            return await JumpToPathAsync(dialogWindowTab, path, isFile, false);
        }

        private static async Task<bool> NavigateDialogPathAsync(HWND hwnd, bool auto = false)
        {
            // Check handle
            if (hwnd == HWND.Null) return false;

            // Get explorer path
            string path;
            lock (_lastExplorerLock)
            {
                path = _lastExplorer?.ExplorerWindow?.GetExplorerPath();
            }
            if (string.IsNullOrEmpty(path)) return false;

            // Check path
            if (!CheckPath(path, out var isFile)) return false;

            // Get dialog tab
            var dialogWindowTab = GetDialogWindowTab(hwnd);
            if (dialogWindowTab == null) return false;

            // Jump to path
            return await JumpToPathAsync(dialogWindowTab, path, isFile, auto);
        }

        private static bool CheckPath(string path, out bool file)
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

        private static IQuickSwitchDialogWindowTab GetDialogWindowTab(HWND hwnd)
        {
            var dialogWindow = GetDialogWindow(hwnd);
            if (dialogWindow == null) return null;
            var dialogWindowTab = dialogWindow.GetCurrentTab();
            return dialogWindowTab;
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

        private static async Task<bool> JumpToPathAsync(IQuickSwitchDialogWindowTab dialog, string path, bool isFile, bool auto = false)
        {
            // Jump after flow launcher window vanished (after JumpAction returned true)
            // and the dialog had been in the foreground.
            var dialogHandle = dialog.Handle;
            var timeOut = !SpinWait.SpinUntil(() => Win32Helper.IsForegroundWindow(dialogHandle), 1000);
            if (timeOut) return false;

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
                            return false;
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
                        _autoSwitchedDialogs.Add(new(dialogHandle));
                    }
                }

                return result;
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to jump to path", e);
                return false;
            }
            finally
            {
                _navigationLock.Release();
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
            if (!_moveSizeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_moveSizeHook);
                _moveSizeHook = HWINEVENTHOOK.Null;
            }
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

            // Dispose locks
            _foregroundChangeLock.Dispose();
            _navigationLock.Dispose();

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
