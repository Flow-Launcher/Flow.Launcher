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

internal static class HotKeyMapper
{
    private static readonly string ClassName = nameof(HotKeyMapper);

    private static Settings _settings;
    private static MainViewModel _mainViewModel;
    private static DoubleTapDetector _doubleTapDetector;

    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetService<Settings>();

        // Check if the main hotkey is a double-tap format (e.g., "Ctrl + Ctrl")
        var mainHotkeyModel = new HotkeyModel(_settings.Hotkey);
        if (mainHotkeyModel.DoubleTap)
        {
            // Register as double-tap hotkey instead of NHotkey
            SetDoubleTapHotkey(_settings.Hotkey);
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

        // Initialize double-tap hotkey if configured (separate from main hotkey)
        if (!string.IsNullOrEmpty(_settings.DoubleTapHotkey))
        {
            SetDoubleTapHotkey(_settings.DoubleTapHotkey);
        }
    }

    internal static void OnToggleHotkey(object sender, HotkeyEventArgs args)
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    internal static void OnToggleHotkeyWithChefKeys()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    private static void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetHotkey(hotkey, action);
    }

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

    private static void RemoveWithChefKeys(string hotkeyStr)
    {
        ChefKeysManager.UnregisterHotkey(hotkeyStr);
        ChefKeysManager.Stop();
    }

    internal static void LoadCustomPluginHotkey()
    {
        if (_settings.CustomPluginHotkeys == null)
            return;

        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            SetCustomQueryHotkey(hotkey);
        }
    }

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
    /// Sets up the double-tap hotkey detector. When the configured key is pressed twice
    /// within the configured interval, the toggle action is triggered.
    /// </summary>
    internal static void SetDoubleTapHotkey(string hotkeyStr)
    {
        App.API.LogDebug(ClassName, $"SetDoubleTapHotkey called with: '{hotkeyStr}'");

        RemoveDoubleTapHotkey();

        if (string.IsNullOrEmpty(hotkeyStr))
        {
            App.API.LogDebug(ClassName, "SetDoubleTapHotkey: hotkeyStr is empty, skipping");
            return;
        }

        if (!DoubleTapDetector.IsValidDoubleTapHotkey(hotkeyStr))
        {
            App.API.LogError(ClassName, $"Invalid double-tap hotkey format: {hotkeyStr}");
            return;
        }

        try
        {
            _doubleTapDetector = new DoubleTapDetector(
                hotkeyStr,
                _settings.DoubleTapHotkeyInterval,
                OnDoubleTapToggleHotkey,
                null // No single-tap action - only double-tap triggers the action
            );
            _doubleTapDetector.Enable();

            App.API.LogDebug(ClassName, $"DoubleTapDetector created and enabled, interval={_settings.DoubleTapHotkeyInterval}ms");

            // Register as a global keyboard handler so we receive key events
            App.API.RegisterGlobalKeyboardCallback(OnGlobalKeyboardEvent);

            App.API.LogDebug(ClassName, "OnGlobalKeyboardEvent registered");
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotKeyMapper.SetDoubleTapHotkey|Error registering double-tap hotkey: {0} \nStackTrace:{1}",
                    e.Message,
                    e.StackTrace));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    /// <summary>
    /// Removes the double-tap hotkey detector.
    /// </summary>
    internal static void RemoveDoubleTapHotkey()
    {
        if (_doubleTapDetector != null)
        {
            App.API.RemoveGlobalKeyboardCallback(OnGlobalKeyboardEvent);
            _doubleTapDetector.Dispose();
            _doubleTapDetector = null;
        }
    }

    /// <summary>
    /// Checks if a double-tap hotkey is available (valid format).
    /// Double-tap hotkeys don't use NHotkey, so availability is based on format validity only.
    /// </summary>
    internal static bool CheckDoubleTapAvailability(string hotkeyStr)
    {
        return DoubleTapDetector.IsValidDoubleTapHotkey(hotkeyStr);
    }

    private static void OnDoubleTapToggleHotkey()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    /// <summary>
    /// Global keyboard callback that forwards events to the DoubleTapDetector.
    /// </summary>
    private static bool OnGlobalKeyboardEvent(int keyEvent, int vkCode, SpecialKeyState state)
    {
        if (_doubleTapDetector != null && _doubleTapDetector.IsEnabled)
        {
            return _doubleTapDetector.ProcessKeyEvent((KeyEvent)keyEvent, vkCode, state);
        }
        return true;
    }

    /// <summary>
    /// Logs the current state of the double-tap detector for debugging.
    /// </summary>
    internal static void LogDoubleTapState()
    {
        if (_doubleTapDetector == null)
        {
            App.API.LogDebug(ClassName, "DoubleTapDetector is null - no double-tap hotkey configured");
        }
        else
        {
            App.API.LogDebug(ClassName,
                $"DoubleTapDetector state: IsEnabled={_doubleTapDetector.IsEnabled}, Hotkey='{_doubleTapDetector.HotkeyString}'");
        }
    }
}
