using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Input;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Hotkey mapper for Avalonia - registers and manages global hotkeys.
/// </summary>
internal static class HotKeyMapper
{
    private static readonly string ClassName = nameof(HotKeyMapper);

    private static Settings? _settings;
    private static MainViewModel? _mainViewModel;
    private static int _toggleHotkeyId = -1;
    private static readonly Dictionary<string, int> _customQueryHotkeyIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initialize the hotkey system and register configured hotkeys.
    /// </summary>
    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetService<Settings>();

        if (_settings == null)
        {
            Log.Warn(ClassName, "Settings not available, using default hotkey");
            return;
        }

        // Initialize the global hotkey system
        GlobalHotkey.Initialize();

        // Register the main toggle hotkey
        SetToggleHotkey(_settings.Hotkey);

        LoadCustomPluginHotkeys();

        Log.Info(ClassName, $"HotKeyMapper initialized with hotkey: {_settings.Hotkey}");
    }

    /// <summary>
    /// Set or update the toggle hotkey.
    /// </summary>
    internal static void SetToggleHotkey(string hotkeyString)
    {
        RemoveToggleHotkey();

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            Log.Warn(ClassName, "Empty hotkey string");
            return;
        }

        var (mods, key) = GlobalHotkey.ParseHotkeyString(hotkeyString);
        
        if (key == 0)
        {
            Log.Error(ClassName, $"Failed to parse hotkey: {hotkeyString}");
            return;
        }

        _toggleHotkeyId = GlobalHotkey.Register(mods, key, OnToggleHotkey);
        
        if (_toggleHotkeyId < 0)
        {
            Log.Error(ClassName, $"Failed to register hotkey: {hotkeyString}");
        }
        else
        {
            Log.Info(ClassName, $"Registered toggle hotkey: {hotkeyString}");
        }
    }

    /// <summary>
    /// Remove the current toggle hotkey.
    /// </summary>
    internal static void RemoveToggleHotkey()
    {
        if (_toggleHotkeyId >= 0)
        {
            GlobalHotkey.Unregister(_toggleHotkeyId);
            _toggleHotkeyId = -1;
        }
    }

    internal static void LoadCustomPluginHotkeys()
    {
        if (_settings?.CustomPluginHotkeys is null)
        {
            return;
        }

        foreach (var customHotkey in _settings.CustomPluginHotkeys)
        {
            if (!SetCustomQueryHotkey(customHotkey))
            {
                Log.Warn(ClassName, $"Failed to load custom query hotkey '{customHotkey.Hotkey}' for query '{customHotkey.ActionKeyword}'");
            }
        }
    }

    internal static bool SetCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey.Hotkey) || string.IsNullOrWhiteSpace(hotkey.ActionKeyword))
        {
            return false;
        }

        RemoveHotkey(hotkey.Hotkey);

        if (!TryRegisterHotkey(hotkey.Hotkey, () =>
            {
                _mainViewModel?.ShowWithInjectedQuery(hotkey.ActionKeyword);
            }, out var hotkeyId))
        {
            return false;
        }

        _customQueryHotkeyIds[hotkey.Hotkey] = hotkeyId;
        return true;
    }

    internal static void RemoveHotkey(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return;
        }

        if (_customQueryHotkeyIds.TryGetValue(hotkeyString, out var hotkeyId))
        {
            GlobalHotkey.Unregister(hotkeyId);
            _customQueryHotkeyIds.Remove(hotkeyString);
        }
    }

    private static void OnToggleHotkey()
    {
        Log.Info(ClassName, "Toggle hotkey triggered");
        _mainViewModel?.ToggleFlowLauncher();
    }

    /// <summary>
    /// Checks if a hotkey is available for registration.
    /// </summary>
    internal static bool CheckAvailability(HotkeyModel hotkey)
    {
        if (!TryGetRegistrationParts(hotkey, out var mods, out var key))
            return false;

        // Try to register and immediately unregister
        int id = GlobalHotkey.Register(mods, key, () => { });
        if (id >= 0)
        {
            GlobalHotkey.Unregister(id);
            return true;
        }

        return false;
    }

    private static bool TryRegisterHotkey(string hotkeyString, Action callback, out int hotkeyId)
    {
        hotkeyId = -1;

        if (!TryGetRegistrationParts(new HotkeyModel(hotkeyString), out var modifiers, out var key))
        {
            Log.Error(ClassName, $"Failed to parse hotkey: {hotkeyString}");
            return false;
        }

        hotkeyId = GlobalHotkey.Register(modifiers, key, callback);

        if (hotkeyId < 0)
        {
            Log.Error(ClassName, $"Failed to register hotkey: {hotkeyString}");
            return false;
        }

        return true;
    }

    private static bool TryGetRegistrationParts(HotkeyModel hotkey, out GlobalHotkey.Modifiers modifiers, out uint key)
    {
        modifiers = GlobalHotkey.Modifiers.None;
        key = 0;

        if (!hotkey.Validate(true))
        {
            return false;
        }

        if (hotkey.Alt)
        {
            modifiers |= GlobalHotkey.Modifiers.Alt;
        }

        if (hotkey.Ctrl)
        {
            modifiers |= GlobalHotkey.Modifiers.Control;
        }

        if (hotkey.Shift)
        {
            modifiers |= GlobalHotkey.Modifiers.Shift;
        }

        if (hotkey.Win)
        {
            modifiers |= GlobalHotkey.Modifiers.Win;
        }

        key = (uint)KeyInterop.VirtualKeyFromKey(hotkey.CharKey);
        return key != 0;
    }

    /// <summary>
    /// Cleanup and unregister all hotkeys.
    /// </summary>
    internal static void Shutdown()
    {
        RemoveToggleHotkey();

        foreach (var hotkeyId in _customQueryHotkeyIds.Values)
        {
            GlobalHotkey.Unregister(hotkeyId);
        }

        _customQueryHotkeyIds.Clear();
        GlobalHotkey.Shutdown();
        Log.Info(ClassName, "HotKeyMapper shutdown");
    }
}
