using System;
using System.Windows.Input;
using Flow.Launcher.Infrastructure;
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
    // Keyboard-hook fallback for a Win+key toggle hotkey that Windows refuses to register.
    // Only one may exist: each instance suppresses and replays the Win key on its own.
    private static string _winComboHotkeyStr;
    private static Func<int, int, SpecialKeyState, bool> _winComboCallback;

    /// <summary>
    /// While true, the Win+key fallback lets all keys through untouched, so the hotkey
    /// dialog can record a combination even when the current hotkey is a Win+key combo.
    /// </summary>
    internal static bool SuspendWinComboHotkey { get; set; }

    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetService<Settings>();

        SetHotkey(_settings.Hotkey, OnToggleHotkey);
        if (_settings.EnableDialogJump)
        {
            SetHotkey(_settings.DialogJumpHotkey, DialogJump.OnToggleHotkey);
        }
        LoadCustomPluginHotkey();
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
            // Windows reserves many Win+key combos (Win+A, Win+D, …). For the main toggle
            // hotkey only, fall back to the keyboard hook instead of failing.
            if (hotkey.Win && hotkey.CharKey != Key.None
                && action == (EventHandler<HotkeyEventArgs>)OnToggleHotkey)
            {
                App.API.LogDebug(ClassName,
                    $"|HotkeyMapper.SetHotkey|RegisterHotKey failed for {hotkeyStr} ({e.Message}); falling back to global keyboard callback.");
                try
                {
                    SetWithGlobalCallback(hotkey, action);
                }
                catch (Exception fallbackEx)
                {
                    App.API.LogError(ClassName,
                        string.Format("|HotkeyMapper.SetHotkey|Fallback global callback registration also failed for {2}: {0} \nStackTrace:{1}",
                                      fallbackEx.Message,
                                      fallbackEx.StackTrace,
                                      hotkeyStr));
                    App.API.ShowMsgBox(Localize.registerHotkeyFailed(hotkeyStr), Localize.MessageBoxTitle());
                }
                return;
            }

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

    private static void SetWithGlobalCallback(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
    {
        const ushort VK_LWIN = 0x5B;
        const ushort VK_RWIN = 0x5C;

        RemoveWinComboCallback();

        int expectedVkCode = KeyInterop.VirtualKeyFromKey(hotkey.CharKey);
        bool needCtrl = hotkey.Ctrl;
        bool needAlt = hotkey.Alt;
        bool needShift = hotkey.Shift;

        // State tracking for Win-key suppression and replay
        bool winSuppressed = false;
        ushort pressedWinVk = 0;
        bool comboFired = false;
        bool winReplayed = false;
        bool skipNextWinDown = false;
        bool skipNextWinUp = false;
        int skipNextKeyCode = 0;

        Func<int, int, SpecialKeyState, bool> callback = (keyEvent, vkCode, state) =>
        {
            bool isDown = keyEvent == (int)KeyEvent.WM_KEYDOWN || keyEvent == (int)KeyEvent.WM_SYSKEYDOWN;
            bool isUp = keyEvent == (int)KeyEvent.WM_KEYUP || keyEvent == (int)KeyEvent.WM_SYSKEYUP;
            bool isWin = vkCode == VK_LWIN || vkCode == VK_RWIN;

            // Skip sentinels: ignore events we injected ourselves
            if (isDown && isWin && skipNextWinDown) { skipNextWinDown = false; return true; }
            if (isUp && isWin && skipNextWinUp) { skipNextWinUp = false; return true; }
            if (isDown && skipNextKeyCode != 0 && vkCode == skipNextKeyCode) { skipNextKeyCode = 0; return true; }

            // Suppress Win keydown and start tracking
            if (isDown && isWin && !winSuppressed)
            {
                // In Game Mode or over a fullscreen app the hotkey would be ignored anyway, and
                // while the hotkey dialog is recording it must see the real keys, so don't
                // intercept or inject anything: leave the Win key sequence untouched.
                if (SuspendWinComboHotkey || _mainViewModel.ShouldIgnoreHotkeys())
                    return true;

                winSuppressed = true;
                pressedWinVk = (ushort)vkCode;
                comboFired = false;
                winReplayed = false;
                return false;
            }

            // Suppress repeated Win keydowns (auto-repeat or both Win keys held simultaneously)
            // while already tracking, to prevent the second keydown from reaching the system.
            if (isDown && isWin && winSuppressed)
                return false;

            // Win keyup while tracking
            if (isUp && isWin && winSuppressed && vkCode == pressedWinVk)
            {
                ushort winVk = pressedWinVk;
                bool fired = comboFired;
                bool replayed = winReplayed;

                winSuppressed = false;
                pressedWinVk = 0;
                comboFired = false;
                winReplayed = false;

                if (fired)
                {
                    if (replayed)
                    {
                        // Win-down was injected during replay; clean it up so Win doesn't stay
                        // stuck pressed in the system key state.
                        skipNextWinUp = true;
                        Win32Helper.InjectKeyUp(winVk);
                    }
                    return false;
                }

                if (replayed)
                {
                    // Already injected Win+key, close the sequence with Win up
                    skipNextWinUp = true;
                    Win32Helper.InjectKeyUp(winVk);
                    return false;
                }

                // Bare Win press — replay Win down+up so Start Menu and Win-only shortcuts still work
                skipNextWinDown = true;
                skipNextWinUp = true;
                Win32Helper.InjectKeyDown(winVk);
                Win32Helper.InjectKeyUp(winVk);
                return false;
            }

            // While Win is suppressed and a non-Win key arrives
            if (winSuppressed && isDown)
            {
                bool isModifier = vkCode is 0x10 or 0x11 or 0x12  // Shift, Ctrl, Alt
                    or 0xA0 or 0xA1  // LShift, RShift
                    or 0xA2 or 0xA3  // LCtrl, RCtrl
                    or 0xA4 or 0xA5; // LAlt, RAlt

                if (isModifier) return true; // let modifier keys flow naturally

                // Note: state.WinPressed is false because we suppressed Win keydown,
                // so we use winSuppressed here instead.
                bool isOurHotkey = vkCode == expectedVkCode
                    && state.CtrlPressed == needCtrl
                    && state.AltPressed == needAlt
                    && state.ShiftPressed == needShift;

                if (isOurHotkey && !comboFired)
                {
                    comboFired = true;
                    // Defer out of the WH_KEYBOARD_LL callback: the hook must return quickly
                    // (or Windows ignores its result and lets the key through, e.g. opening
                    // Quick Settings for Win+A), and the window can only take foreground
                    // after the swallowed keystroke has finished being processed.
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => action?.Invoke(null, null));
                    return false;
                }

                if (!comboFired && !winReplayed)
                {
                    // Not our hotkey — replay Win+key so system shortcuts (Win+D, Win+L, …) still work
                    winReplayed = true;
                    skipNextWinDown = true;
                    skipNextKeyCode = vkCode;
                    Win32Helper.InjectKeyDown(pressedWinVk);
                    Win32Helper.InjectKeyDown((ushort)vkCode);
                    return false;
                }
            }

            // Suppress the char key-up that matches the suppressed key-down
            if (comboFired && isUp && vkCode == expectedVkCode)
                return false;

            return true;
        };

        _winComboHotkeyStr = hotkey.ToString();
        _winComboCallback = callback;
        App.API.RegisterGlobalKeyboardCallback(callback);
    }

    private static void RemoveWinComboCallback()
    {
        if (_winComboCallback == null)
            return;

        App.API.RemoveGlobalKeyboardCallback(_winComboCallback);
        _winComboCallback = null;
        _winComboHotkeyStr = null;
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

            if (_winComboCallback != null && hotkeyStr == _winComboHotkeyStr)
            {
                RemoveWinComboCallback();
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
}
