using System;
using ChefKeys;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using NHotkey;
using NHotkey.Wpf;

namespace Flow.Launcher.Helper;

/// <summary>
/// Manages the registration, removal, and routing of all hotkeys in Flow Launcher,
/// including NHotkey-based combo hotkeys, ChefKeys-based Win hotkeys,
/// and double-tap hotkeys detected via the global keyboard hook.
/// </summary>
internal static class HotKeyMapper
{
    private static readonly string ClassName = nameof(HotKeyMapper);

    private static Settings _settings;
    private static MainViewModel _mainViewModel;

    /// <summary>
    /// Double-tap detector for the main toggle hotkey when it is in double-tap format
    /// (e.g., "Ctrl + Ctrl"). Null if the main hotkey is a regular combo hotkey.
    /// </summary>
    private static DoubleTapDetector _mainDoubleTapDetector;

    /// <summary>
    /// Double-tap detector for the dedicated DoubleTapHotkey setting.
    /// Null if no separate double-tap hotkey is configured.
    /// Independent from <see cref="_mainDoubleTapDetector"/> — both can be active simultaneously.
    /// </summary>
    private static DoubleTapDetector _separateDoubleTapDetector;

    /// <summary>
    /// Initializes all hotkey registrations from the current settings.
    /// Registers the main toggle hotkey (combo or double-tap), dialog jump hotkey,
    /// custom plugin hotkeys, and the separate double-tap hotkey if configured.
    /// </summary>
    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetService<Settings>();

        // Check if the main hotkey is a double-tap format (e.g., "Ctrl + Ctrl")
        var mainHotkeyModel = new HotkeyModel(_settings.Hotkey);
        if (mainHotkeyModel.DoubleTap)
        {
            // Register as double-tap hotkey instead of NHotkey
            SetMainDoubleTapHotkey(_settings.Hotkey);
        }
        else
        {
            SetHotkey(_settings.Hotkey, OnToggleHotkey);
        }

        if (_settings.EnableDialogJump)
        {
            SetHotkey(_settings.DialogJumpHotkey, DialogJump.OnToggleHotkey);
        }
        LoadCustomPluginHotkey();

        // Initialize separate double-tap hotkey if configured (independent from main hotkey)
        if (!string.IsNullOrEmpty(_settings.DoubleTapHotkey))
        {
            SetSeparateDoubleTapHotkey(_settings.DoubleTapHotkey);
        }
    }

    /// <summary>
    /// Callback invoked when the main toggle hotkey is pressed via NHotkey.
    /// </summary>
    internal static void OnToggleHotkey(object sender, HotkeyEventArgs args)
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    /// <summary>
    /// Callback invoked when the main toggle hotkey is pressed via ChefKeys (Win key).
    /// </summary>
    internal static void OnToggleHotkeyWithChefKeys()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    /// <summary>
    /// Registers a combo hotkey string with the given action via NHotkey or ChefKeys.
    /// </summary>
    private static void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetHotkey(hotkey, action);
    }

    /// <summary>
    /// Registers a combo hotkey with ChefKeys (used for Win key bindings that NHotkey cannot handle).
    /// </summary>
    private static void SetWithChefKeys(string hotkeyStr)
    {
        try
        {
            ChefKeysManager.RegisterHotkey(hotkeyStr, hotkeyStr, OnToggleHotkeyWithChefKeys);
            ChefKeysManager.Start();
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.SetWithChefKeys|Error registering hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    /// <summary>
    /// Registers a combo hotkey with NHotkey. Falls back to ChefKeys for Win key bindings.
    /// </summary>
    internal static void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
    {
        string hotkeyStr = hotkey.ToString();
        try
        {
            if (hotkeyStr == "LWin" || hotkeyStr == "RWin")
            {
                SetWithChefKeys(hotkeyStr);
                return;
            }

            HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.SetHotkey|Error registering hotkey {2}: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace,
                              hotkeyStr));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    /// <summary>
    /// Removes a combo hotkey registration. Uses ChefKeys for Win key bindings, NHotkey otherwise.
    /// </summary>
    internal static void RemoveHotkey(string hotkeyStr)
    {
        try
        {
            if (hotkeyStr == "LWin" || hotkeyStr == "RWin")
            {
                RemoveWithChefKeys(hotkeyStr);
                return;
            }

            if (!string.IsNullOrEmpty(hotkeyStr))
                HotkeyManager.Current.Remove(hotkeyStr);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.RemoveHotkey|Error removing hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = Localize.unregisterHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    /// <summary>
    /// Unregisters a ChefKeys-based hotkey and stops the ChefKeys manager.
    /// </summary>
    private static void RemoveWithChefKeys(string hotkeyStr)
    {
        ChefKeysManager.UnregisterHotkey(hotkeyStr);
        ChefKeysManager.Stop();
    }

    /// <summary>
    /// Loads and registers all custom plugin hotkeys from settings.
    /// </summary>
    internal static void LoadCustomPluginHotkey()
    {
        if (_settings.CustomPluginHotkeys == null)
            return;

        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            SetCustomQueryHotkey(hotkey);
        }
    }

    /// <summary>
    /// Registers a custom query hotkey that opens the main window with a specific action keyword.
    /// </summary>
    internal static void SetCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        SetHotkey(hotkey.Hotkey, (s, e) =>
        {
            if (_mainViewModel.ShouldIgnoreHotkeys())
                return;

            App.API.ShowMainWindow();
            // Make sure to go back to the query results page first since it can cause issues if current page is context menu
            App.API.BackToQueryResults();
            App.API.ChangeQuery(hotkey.ActionKeyword, true);
        });
    }

    /// <summary>
    /// Checks if a combo hotkey is available for registration by temporarily registering it.
    /// </summary>
    internal static bool CheckAvailability(HotkeyModel currentHotkey)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace("HotkeyAvailabilityTest", currentHotkey.CharKey, currentHotkey.ModifierKeys, (sender, e) => { });

            return true;
        }
        catch
        {
        }
        finally
        {
            HotkeyManager.Current.Remove("HotkeyAvailabilityTest");
        }

        return false;
    }

    /// <summary>
    /// Sets up the main toggle hotkey as a double-tap detector.
    /// This is used when the main hotkey is in double-tap format (e.g., "Ctrl + Ctrl").
    /// Removes any existing main double-tap detector before creating a new one.
    /// </summary>
    /// <param name="hotkeyStr">The double-tap hotkey string (e.g., "Ctrl + Ctrl").</param>
    internal static void SetMainDoubleTapHotkey(string hotkeyStr)
    {
        App.API.LogDebug(ClassName, $"SetMainDoubleTapHotkey called with: '{hotkeyStr}'");

        RemoveMainDoubleTapHotkey();

        if (string.IsNullOrEmpty(hotkeyStr))
        {
            App.API.LogDebug(ClassName, "SetMainDoubleTapHotkey: hotkeyStr is empty, skipping");
            return;
        }

        if (!DoubleTapDetector.IsValidDoubleTapHotkey(hotkeyStr))
        {
            App.API.LogError(ClassName, $"Invalid double-tap hotkey format: {hotkeyStr}");
            return;
        }

        try
        {
            _mainDoubleTapDetector = new DoubleTapDetector(
                hotkeyStr,
                _settings.DoubleTapHotkeyInterval,
                OnDoubleTapToggleHotkey,
                null
            );
            _mainDoubleTapDetector.Enable();

            EnsureGlobalKeyboardCallbackRegistered();

            App.API.LogDebug(ClassName, $"Main DoubleTapDetector created and enabled, interval={_settings.DoubleTapHotkeyInterval}ms");
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotKeyMapper.SetMainDoubleTapHotkey|Error: {0} \nStackTrace:{1}",
                    e.Message, e.StackTrace));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    /// <summary>
    /// Removes the main toggle double-tap detector.
    /// </summary>
    internal static void RemoveMainDoubleTapHotkey()
    {
        if (_mainDoubleTapDetector != null)
        {
            _mainDoubleTapDetector.Dispose();
            _mainDoubleTapDetector = null;

            RemoveGlobalKeyboardCallbackIfUnused();

            App.API.LogDebug(ClassName, "Main DoubleTapDetector removed");
        }
    }

    /// <summary>
    /// Sets up the separate double-tap hotkey detector from the DoubleTapHotkey setting.
    /// This is independent from the main toggle hotkey — both can be active simultaneously.
    /// Removes any existing separate double-tap detector before creating a new one.
    /// </summary>
    /// <param name="hotkeyStr">The double-tap hotkey string (e.g., "Alt + Alt").</param>
    internal static void SetSeparateDoubleTapHotkey(string hotkeyStr)
    {
        App.API.LogDebug(ClassName, $"SetSeparateDoubleTapHotkey called with: '{hotkeyStr}'");

        RemoveSeparateDoubleTapHotkey();

        if (string.IsNullOrEmpty(hotkeyStr))
        {
            App.API.LogDebug(ClassName, "SetSeparateDoubleTapHotkey: hotkeyStr is empty, skipping");
            return;
        }

        if (!DoubleTapDetector.IsValidDoubleTapHotkey(hotkeyStr))
        {
            App.API.LogError(ClassName, $"Invalid double-tap hotkey format: {hotkeyStr}");
            return;
        }

        try
        {
            _separateDoubleTapDetector = new DoubleTapDetector(
                hotkeyStr,
                _settings.DoubleTapHotkeyInterval,
                OnDoubleTapToggleHotkey,
                null
            );
            _separateDoubleTapDetector.Enable();

            EnsureGlobalKeyboardCallbackRegistered();

            App.API.LogDebug(ClassName, $"Separate DoubleTapDetector created and enabled, interval={_settings.DoubleTapHotkeyInterval}ms");
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotKeyMapper.SetSeparateDoubleTapHotkey|Error: {0} \nStackTrace:{1}",
                    e.Message, e.StackTrace));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    /// <summary>
    /// Removes the separate double-tap hotkey detector.
    /// </summary>
    internal static void RemoveSeparateDoubleTapHotkey()
    {
        if (_separateDoubleTapDetector != null)
        {
            _separateDoubleTapDetector.Dispose();
            _separateDoubleTapDetector = null;

            RemoveGlobalKeyboardCallbackIfUnused();

            App.API.LogDebug(ClassName, "Separate DoubleTapDetector removed");
        }
    }

    /// <summary>
    /// Sets a double-tap hotkey by routing to the appropriate detector based on context.
    /// When called from the main toggle hotkey control, registers the main double-tap detector.
    /// When called from the dedicated DoubleTapHotkey control, registers the separate detector.
    /// </summary>
    /// <param name="hotkeyStr">The double-tap hotkey string.</param>
    /// <param name="isMainHotkey">
    /// True if this is the main toggle hotkey being set as double-tap;
    /// false if this is the dedicated DoubleTapHotkey setting.
    /// </param>
    internal static void SetDoubleTapHotkey(string hotkeyStr, bool isMainHotkey = false)
    {
        if (isMainHotkey)
        {
            SetMainDoubleTapHotkey(hotkeyStr);
        }
        else
        {
            SetSeparateDoubleTapHotkey(hotkeyStr);
        }
    }

    /// <summary>
    /// Removes a double-tap hotkey by routing to the appropriate detector based on context.
    /// </summary>
    /// <param name="isMainHotkey">
    /// True to remove the main toggle double-tap detector;
    /// false to remove the separate DoubleTapHotkey detector.
    /// </param>
    internal static void RemoveDoubleTapHotkey(bool isMainHotkey = false)
    {
        if (isMainHotkey)
        {
            RemoveMainDoubleTapHotkey();
        }
        else
        {
            RemoveSeparateDoubleTapHotkey();
        }
    }

    /// <summary>
    /// Removes all double-tap detectors (both main and separate).
    /// </summary>
    internal static void RemoveAllDoubleTapHotkeys()
    {
        RemoveMainDoubleTapHotkey();
        RemoveSeparateDoubleTapHotkey();
    }

    /// <summary>
    /// Checks if a double-tap hotkey is available (valid format).
    /// Double-tap hotkeys don't use NHotkey, so availability is based on format validity only.
    /// </summary>
    internal static bool CheckDoubleTapAvailability(string hotkeyStr)
    {
        return DoubleTapDetector.IsValidDoubleTapHotkey(hotkeyStr);
    }

    /// <summary>
    /// Callback invoked when any double-tap hotkey is detected.
    /// Toggles the Flow Launcher main window.
    /// </summary>
    private static void OnDoubleTapToggleHotkey()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    /// <summary>
    /// Ensures the global keyboard callback is registered.
    /// Called when any double-tap detector is created. The callback is shared across all detectors.
    /// </summary>
    private static void EnsureGlobalKeyboardCallbackRegistered()
    {
        App.API.RegisterGlobalKeyboardCallback(OnGlobalKeyboardEvent);
        App.API.LogDebug(ClassName, "OnGlobalKeyboardEvent registered");
    }

    /// <summary>
    /// Removes the global keyboard callback if no double-tap detectors are active.
    /// This avoids unnecessary keyboard hook processing when no double-tap hotkeys are configured.
    /// </summary>
    private static void RemoveGlobalKeyboardCallbackIfUnused()
    {
        if (_mainDoubleTapDetector == null && _separateDoubleTapDetector == null)
        {
            App.API.RemoveGlobalKeyboardCallback(OnGlobalKeyboardEvent);
            App.API.LogDebug(ClassName, "OnGlobalKeyboardEvent removed (no active detectors)");
        }
    }

    /// <summary>
    /// Global keyboard callback that forwards events to all active DoubleTapDetectors.
    /// Each detector independently checks if the event matches its target key.
    /// Returns false if any detector consumed the event (double-tap detected),
    /// true to allow the event to continue to other handlers.
    /// </summary>
    private static bool OnGlobalKeyboardEvent(int keyEvent, int vkCode, SpecialKeyState state)
    {
        bool consumed = false;

        if (_mainDoubleTapDetector != null && _mainDoubleTapDetector.IsEnabled)
        {
            if (!_mainDoubleTapDetector.ProcessKeyEvent((KeyEvent)keyEvent, vkCode, state))
                consumed = true;
        }

        if (_separateDoubleTapDetector != null && _separateDoubleTapDetector.IsEnabled)
        {
            if (!_separateDoubleTapDetector.ProcessKeyEvent((KeyEvent)keyEvent, vkCode, state))
                consumed = true;
        }

        return !consumed;
    }

    /// <summary>
    /// Logs the current state of both double-tap detectors for debugging.
    /// </summary>
    internal static void LogDoubleTapState()
    {
        if (_mainDoubleTapDetector == null && _separateDoubleTapDetector == null)
        {
            App.API.LogDebug(ClassName, "No double-tap hotkeys configured");
        }
        else
        {
            if (_mainDoubleTapDetector != null)
            {
                App.API.LogDebug(ClassName,
                    $"Main DoubleTapDetector: IsEnabled={_mainDoubleTapDetector.IsEnabled}, Hotkey='{_mainDoubleTapDetector.HotkeyString}'");
            }
            if (_separateDoubleTapDetector != null)
            {
                App.API.LogDebug(ClassName,
                    $"Separate DoubleTapDetector: IsEnabled={_separateDoubleTapDetector.IsEnabled}, Hotkey='{_separateDoubleTapDetector.HotkeyString}'");
            }
        }
    }
}
