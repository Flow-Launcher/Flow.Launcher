using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.DialogJump.Models;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using NHotkey;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Flow.Launcher.Infrastructure.DialogJump
{
    public static class DialogJump
    {
        #region Public Properties

        public static Func<nint, Task> ShowDialogJumpWindowAsync { get; set; } = null;

        public static Action UpdateDialogJumpWindow { get; set; } = null;

        public static Action ResetDialogJumpWindow { get; set; } = null;

        public static Action HideDialogJumpWindow { get; set; } = null;

        public static DialogJumpWindowPositions DialogJumpWindowPosition { get; private set; }

        public static DialogJumpExplorerPair WindowsDialogJumpExplorer { get; } = new()
        {
            Metadata = new()
            {
                ID = "298b197c08a24e90ab66ac060ee2b6b8", // ID is for calculating the hash id of the Dialog Jump pairs
                Disabled = false // Disabled is for enabling the Windows DialogJump explorers & dialogs
            },
            Plugin = new WindowsExplorer()
        };

        public static DialogJumpDialogPair WindowsDialogJumpDialog { get; } = new()
        {
            Metadata = new()
            {
                ID = "a4a113dc51094077ab4abb391e866c7b", // ID is for calculating the hash id of the Dialog Jump pairs
                Disabled = false // Disabled is for enabling the Windows DialogJump explorers & dialogs
            },
            Plugin = new WindowsDialog()
        };

        #endregion

        #region Private Fields

        private static readonly string ClassName = nameof(DialogJump);

        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        private static HWND _mainWindowHandle = HWND.Null;

        private static readonly Dictionary<DialogJumpExplorerPair, IDialogJumpExplorerWindow> _dialogJumpExplorers = new();

        private static DialogJumpExplorerPair _lastExplorer = null;
        private static readonly object _lastExplorerLock = new();

        private static readonly Dictionary<DialogJumpDialogPair, IDialogJumpDialogWindow> _dialogJumpDialogs = new();

        private static IDialogJumpDialogWindow _dialogWindow = null;
        private static readonly object _dialogWindowLock = new();

        private static HWINEVENTHOOK _foregroundChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _locationChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _destroyChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _hideChangeHook = HWINEVENTHOOK.Null;
        private static HWINEVENTHOOK _dialogEndChangeHook = HWINEVENTHOOK.Null;

        private static readonly WINEVENTPROC _fgProc = ForegroundChangeCallback;
        private static readonly WINEVENTPROC _locProc = LocationChangeCallback;
        private static readonly WINEVENTPROC _desProc = DestroyChangeCallback;
        private static readonly WINEVENTPROC _hideProc = HideChangeCallback;
        private static readonly WINEVENTPROC _dialogEndProc = DialogEndChangeCallback;

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

        public static void InitializeDialogJump(IList<DialogJumpExplorerPair> dialogJumpExplorers,
            IList<DialogJumpDialogPair> dialogJumpDialogs)
        {
            if (_initialized) return;

            // Initialize Dialog Jump explorers & dialogs
            _dialogJumpExplorers.Add(WindowsDialogJumpExplorer, null);
            foreach (var explorer in dialogJumpExplorers)
            {
                _dialogJumpExplorers.Add(explorer, null);
            }
            _dialogJumpDialogs.Add(WindowsDialogJumpDialog, null);
            foreach (var dialog in dialogJumpDialogs)
            {
                _dialogJumpDialogs.Add(dialog, null);
            }

            // Initialize main window handle
            _mainWindowHandle = Win32Helper.GetMainWindowHandle();

            // Initialize timer
            _dragMoveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _dragMoveTimer.Tick += (s, e) => InvokeUpdateDialogJumpWindow();

            // Initialize Dialog Jump window position
            DialogJumpWindowPosition = _settings.DialogJumpWindowPosition;

            _initialized = true;
        }

        public static void SetupDialogJump(bool enabled)
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
                if (!_hideChangeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_hideChangeHook);
                    _hideChangeHook = HWINEVENTHOOK.Null;
                }
                if (!_dialogEndChangeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_dialogEndChangeHook);
                    _dialogEndChangeHook = HWINEVENTHOOK.Null;
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
                _hideChangeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_OBJECT_HIDE,
                    PInvoke.EVENT_OBJECT_HIDE,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    _hideProc,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
                _dialogEndChangeHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_SYSTEM_DIALOGEND,
                    PInvoke.EVENT_SYSTEM_DIALOGEND,
                    PInvoke.GetModuleHandle((PCWSTR)null),
                    _dialogEndProc,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);

                if (_foregroundChangeHook.IsNull ||
                    _locationChangeHook.IsNull ||
                    _destroyChangeHook.IsNull ||
                    _hideChangeHook.IsNull ||
                    _dialogEndChangeHook.IsNull)
                {
                    Log.Error(ClassName, "Failed to enable DialogJump");
                    return;
                }
            }
            else
            {
                // Remove explorer windows
                foreach (var explorer in _dialogJumpExplorers.Keys)
                {
                    _dialogJumpExplorers[explorer] = null;
                }

                // Remove dialog windows
                foreach (var dialog in _dialogJumpDialogs.Keys)
                {
                    _dialogJumpDialogs[dialog] = null;
                }

                // Remove dialog window handle
                var dialogWindowExists = false;
                lock (_dialogWindowLock)
                {
                    if (_dialogWindow != null)
                    {
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
                if (!_hideChangeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_hideChangeHook);
                    _hideChangeHook = HWINEVENTHOOK.Null;
                }
                if (!_dialogEndChangeHook.IsNull)
                {
                    PInvoke.UnhookWinEvent(_dialogEndChangeHook);
                    _dialogEndChangeHook = HWINEVENTHOOK.Null;
                }

                // Stop drag move timer
                _dragMoveTimer?.Stop();

                // Reset Dialog Jump window
                if (dialogWindowExists)
                {
                    InvokeResetDialogJumpWindow();
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
                    foreach (var explorer in _dialogJumpExplorers.Keys)
                    {
                        if (API.PluginModified(explorer.Metadata.ID) || // Plugin is modified
                            explorer.Metadata.Disabled) continue; // Plugin is disabled

                        var explorerWindow = explorer.Plugin.CheckExplorerWindow(hWnd);
                        if (explorerWindow != null)
                        {
                            _dialogJumpExplorers[explorer] = explorerWindow;
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
            return RefreshLastExplorer() ? _dialogJumpExplorers[_lastExplorer].GetExplorerPath() : string.Empty;
        }

        #endregion

        #region Events

        #region Invoke Property Events

        private static async Task InvokeShowDialogJumpWindowAsync(bool dialogWindowChanged)
        {
            // Show Dialog Jump window
            if (_settings.ShowDialogJumpWindow)
            {
                // Save Dialog Jump window position for one file dialog
                if (dialogWindowChanged)
                {
                    DialogJumpWindowPosition = _settings.DialogJumpWindowPosition;
                }

                // Call show Dialog Jump window
                IDialogJumpDialogWindow dialogWindow;
                lock (_dialogWindowLock)
                {
                    dialogWindow = _dialogWindow;
                }
                if (dialogWindow != null && ShowDialogJumpWindowAsync != null)
                {
                    await ShowDialogJumpWindowAsync.Invoke(dialogWindow.Handle);
                }

                // Hook move size event if Dialog Jump window is under dialog & dialog window changed
                if (DialogJumpWindowPosition == DialogJumpWindowPositions.UnderDialog)
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

        private static void InvokeUpdateDialogJumpWindow()
        {
            UpdateDialogJumpWindow?.Invoke();
        }

        private static void InvokeResetDialogJumpWindow()
        {
            lock (_dialogWindowLock)
            {
                _dialogWindow = null;
            }

            // Reset Dialog Jump window
            ResetDialogJumpWindow?.Invoke();

            // Stop drag move timer
            _dragMoveTimer?.Stop();

            // Unhook move size event
            if (!_moveSizeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_moveSizeHook);
                _moveSizeHook = HWINEVENTHOOK.Null;
            }
        }

        private static void InvokeHideDialogJumpWindow()
        {
            // Hide Dialog Jump window
            HideDialogJumpWindow?.Invoke();

            // Stop drag move timer
            _dragMoveTimer?.Stop();
        }

        #endregion

        #region Hotkey

        public static void OnToggleHotkey(object sender, HotkeyEventArgs args)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await NavigateDialogPathAsync(PInvoke.GetForegroundWindow());
                }
                catch (System.Exception ex)
                {
                    Log.Exception(ClassName, "Failed to navigate dialog path", ex);
                }
            });
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
                foreach (var dialog in _dialogJumpDialogs.Keys)
                {
                    if (API.PluginModified(dialog.Metadata.ID) || // Plugin is modified
                        dialog.Metadata.Disabled) continue; // Plugin is disabled

                    IDialogJumpDialogWindow dialogWindow;
                    var existingDialogWindow = _dialogJumpDialogs[dialog];
                    if (existingDialogWindow != null && existingDialogWindow.Handle == hwnd)
                    {
                        // If the dialog window is already in the list, no need to check again
                        dialogWindow = existingDialogWindow;
                    }
                    else
                    {
                        dialogWindow = dialog.Plugin.CheckDialogWindow(hwnd);
                    }

                    // If the dialog window is found, set it
                    if (dialogWindow != null)
                    {
                        lock (_dialogWindowLock)
                        {
                            dialogWindowChanged = _dialogWindow == null || _dialogWindow.Handle != hwnd;
                            _dialogWindow = dialogWindow;
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
                    if (_settings.AutoDialogJump)
                    {
                        // Check if we have already switched for this dialog
                        bool alreadySwitched;
                        lock (_autoSwitchedDialogsLock)
                        {
                            alreadySwitched = _autoSwitchedDialogs.Contains(hwnd);
                        }

                        // Just show Dialog Jump window
                        if (alreadySwitched)
                        {
                            await InvokeShowDialogJumpWindowAsync(dialogWindowChanged);
                        }
                        // Show Dialog Jump window after navigating the path
                        else
                        {
                            if (!await Task.Run(async () =>
                            {
                                try
                                {
                                    return await NavigateDialogPathAsync(hwnd, true);
                                }
                                catch (System.Exception ex)
                                {
                                    Log.Exception(ClassName, "Failed to navigate dialog path", ex);
                                    return false;
                                }
                            }))
                            {
                                await InvokeShowDialogJumpWindowAsync(dialogWindowChanged);
                            }
                        }
                    }
                    else
                    {
                        await InvokeShowDialogJumpWindowAsync(dialogWindowChanged);
                    }
                }
                // Dialog jump window
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
                    if (dialogWindowExist) // Neither Dialog Jump window nor file dialog window is foreground
                    {
                        // Hide Dialog Jump window until the file dialog window is brought to the foreground
                        InvokeHideDialogJumpWindow();
                    }

                    // Check if there are foreground explorer windows
                    try
                    {
                        lock (_lastExplorerLock)
                        {
                            foreach (var explorer in _dialogJumpExplorers.Keys)
                            {
                                if (API.PluginModified(explorer.Metadata.ID) || // Plugin is modified
                                    explorer.Metadata.Disabled) continue; // Plugin is disabled

                                var explorerWindow = explorer.Plugin.CheckExplorerWindow(hwnd);
                                if (explorerWindow != null)
                                {
                                    Log.Debug(ClassName, $"Explorer window: {hwnd}");
                                    _dialogJumpExplorers[explorer] = explorerWindow;
                                    _lastExplorer = explorer;
                                    break;
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Exception(ClassName, "An error occurred while checking foreground explorer windows", ex);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ClassName, "Failed to invoke ForegroundChangeCallback", ex);
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
            // If the dialog window is moved, update the Dialog Jump window position
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
                InvokeUpdateDialogJumpWindow();
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
            // If the dialog window is moved or resized, update the Dialog Jump window position
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
                InvokeResetDialogJumpWindow();
            }
        }

        private static void HideChangeCallback(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            // If the dialog window is hidden, set _dialogWindowHandle to null
            var dialogWindowExist = false;
            lock (_dialogWindowLock)
            {
                if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
                {
                    Log.Debug(ClassName, $"Hide dialog: {hwnd}");
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
                InvokeResetDialogJumpWindow();
            }
        }

        private static void DialogEndChangeCallback(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            // If the dialog window is ended, set _dialogWindowHandle to null
            var dialogWindowExist = false;
            lock (_dialogWindowLock)
            {
                if (_dialogWindow != null && _dialogWindow.Handle == hwnd)
                {
                    Log.Debug(ClassName, $"End dialog: {hwnd}");
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
                InvokeResetDialogJumpWindow();
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

            // Check path null or empty
            if (string.IsNullOrEmpty(path)) return false;

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
                path = _dialogJumpExplorers[_lastExplorer]?.GetExplorerPath();
            }

            // Check path null or empty
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
            try
            {
                // shell: and shell::: paths
                if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // file: URI paths
                string localPath;
                if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to create a URI from the path
                    if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    {
                        localPath = uri.LocalPath;
                    }
                    else
                    {
                        // If URI creation fails, treat it as a regular path
                        // by removing the "file:" prefix
                        localPath = path.Substring(5);
                    }
                }
                else
                {
                    localPath = path;
                }
                // Is folder?
                var isFolder = Directory.Exists(localPath);
                // Is file?
                var isFile = File.Exists(localPath);
                file = isFile;
                return isFolder || isFile;
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to check path", e);
                return false;
            }
        }

        private static IDialogJumpDialogWindowTab GetDialogWindowTab(HWND hwnd)
        {
            var dialogWindow = GetDialogWindow(hwnd);
            if (dialogWindow == null) return null;
            var dialogWindowTab = dialogWindow.GetCurrentTab();
            return dialogWindowTab;
        }

        private static IDialogJumpDialogWindow GetDialogWindow(HWND hwnd)
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
            foreach (var dialog in _dialogJumpDialogs.Keys)
            {
                if (API.PluginModified(dialog.Metadata.ID) || // Plugin is modified
                    dialog.Metadata.Disabled) continue; // Plugin is disabled

                var dialogWindow = _dialogJumpDialogs[dialog];
                if (dialogWindow != null && dialogWindow.Handle == hwnd)
                {
                    return dialogWindow;
                }
            }

            // Finally search for the dialog window again
            foreach (var dialog in _dialogJumpDialogs.Keys)
            {
                if (API.PluginModified(dialog.Metadata.ID) || // Plugin is modified
                    dialog.Metadata.Disabled) continue; // Plugin is disabled

                IDialogJumpDialogWindow dialogWindow;
                var existingDialogWindow = _dialogJumpDialogs[dialog];
                if (existingDialogWindow != null && existingDialogWindow.Handle == hwnd)
                {
                    // If the dialog window is already in the list, no need to check again
                    dialogWindow = existingDialogWindow;
                }
                else
                {
                    dialogWindow = dialog.Plugin.CheckDialogWindow(hwnd);
                }

                // Update dialog window if found
                if (dialogWindow != null)
                {
                    _dialogJumpDialogs[dialog] = dialogWindow;
                    return dialogWindow;
                }
            }

            return null;
        }

        private static async Task<bool> JumpToPathAsync(IDialogJumpDialogWindowTab dialog, string path, bool isFile, bool auto = false)
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
                    switch (_settings.DialogJumpFileResultBehaviour)
                    {
                        case DialogJumpFileResultBehaviours.FullPath:
                            Log.Debug(ClassName, $"File Jump FullPath: {path}");
                            result = FileJump(path, dialog);
                            break;
                        case DialogJumpFileResultBehaviours.FullPathOpen:
                            Log.Debug(ClassName, $"File Jump FullPathOpen: {path}");
                            result = FileJump(path, dialog, openFile: true);
                            break;
                        case DialogJumpFileResultBehaviours.Directory:
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

        private static bool FileJump(string filePath, IDialogJumpDialogWindowTab dialog, bool openFile = false)
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

        private static bool DirJump(string dirPath, IDialogJumpDialogWindowTab dialog, bool auto = false)
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
            if (!_hideChangeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_hideChangeHook);
                _hideChangeHook = HWINEVENTHOOK.Null;
            }
            if (!_dialogEndChangeHook.IsNull)
            {
                PInvoke.UnhookWinEvent(_dialogEndChangeHook);
                _dialogEndChangeHook = HWINEVENTHOOK.Null;
            }

            // Dispose explorers
            foreach (var explorer in _dialogJumpExplorers.Keys)
            {
                _dialogJumpExplorers[explorer]?.Dispose();
            }
            _dialogJumpExplorers.Clear();
            lock (_lastExplorerLock)
            {
                _lastExplorer = null;
            }

            // Dispose dialogs
            foreach (var dialog in _dialogJumpDialogs.Keys)
            {
                _dialogJumpDialogs[dialog]?.Dispose();
            }
            _dialogJumpDialogs.Clear();
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
