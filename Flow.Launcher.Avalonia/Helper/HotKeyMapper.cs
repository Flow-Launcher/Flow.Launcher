using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Hotkey mapper for Avalonia - registers and manages global hotkeys.
/// </summary>
internal static partial class HotKeyMapper
{
    private static readonly string ClassName = nameof(HotKeyMapper);

    private static Settings? _settings;
    private static MainViewModel? _mainViewModel;
    private static int _toggleHotkeyId = -1;
    private static string _toggleHotkeyString = string.Empty;
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
        InitializeHotkeySystem();

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
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            RemoveToggleHotkey();
            Log.Warn(ClassName, "Empty hotkey string");
            return;
        }

        var previousHotkeyId = _toggleHotkeyId;
        var previousHotkeyString = _toggleHotkeyString;

        if (previousHotkeyId >= 0)
        {
            UnregisterHotkey(previousHotkeyId);
            _toggleHotkeyId = -1;
        }

        if (!TryRegisterHotkey(hotkeyString, OnToggleHotkey, out var newHotkeyId))
        {
            Log.Error(ClassName, $"Failed to register hotkey: {hotkeyString}");

            if (!string.IsNullOrWhiteSpace(previousHotkeyString))
            {
                if (TryRegisterHotkey(previousHotkeyString, OnToggleHotkey, out var restoredHotkeyId))
                {
                    _toggleHotkeyId = restoredHotkeyId;
                    if (_toggleHotkeyId >= 0)
                    {
                        _toggleHotkeyString = previousHotkeyString;
                        Log.Warn(ClassName, $"Restored previous toggle hotkey: {previousHotkeyString}");
                    }
                }
            }

            return;
        }

        _toggleHotkeyId = newHotkeyId;
        _toggleHotkeyString = hotkeyString;
        Log.Info(ClassName, $"Registered toggle hotkey: {hotkeyString}");
    }

    /// <summary>
    /// Remove the current toggle hotkey.
    /// </summary>
    internal static void RemoveToggleHotkey()
    {
        if (_toggleHotkeyId >= 0)
        {
            UnregisterHotkey(_toggleHotkeyId);
            _toggleHotkeyId = -1;
        }

        _toggleHotkeyString = string.Empty;
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
                if (ShouldIgnoreHotkeys())
                {
                    return;
                }

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
            UnregisterHotkey(hotkeyId);
            _customQueryHotkeyIds.Remove(hotkeyString);
        }
    }

    private static void OnToggleHotkey()
    {
        if (ShouldIgnoreHotkeys())
        {
            Log.Info(ClassName, "Toggle hotkey ignored");
            return;
        }

        Log.Info(ClassName, "Toggle hotkey triggered");
        _mainViewModel?.ToggleFlowLauncher();
    }

    private static bool ShouldIgnoreHotkeys()
    {
        return _settings?.IgnoreHotkeysOnFullscreen == true && IsForegroundWindowFullscreen()
            || App.API?.IsGameModeOn() == true;
    }

    /// <summary>
    /// Checks if a hotkey is available for registration.
    /// </summary>
    internal static partial bool CheckAvailability(HotkeyModel hotkey);

    private static partial bool TryRegisterHotkey(string hotkeyString, Action callback, out int hotkeyId);

    private static partial bool IsForegroundWindowFullscreen();

    static partial void InitializeHotkeySystem();

    static partial void UnregisterHotkey(int hotkeyId);

    static partial void ShutdownHotkeySystem();

    /// <summary>
    /// Cleanup and unregister all hotkeys.
    /// </summary>
    internal static void Shutdown()
    {
        RemoveToggleHotkey();

        foreach (var hotkeyId in _customQueryHotkeyIds.Values)
        {
            UnregisterHotkey(hotkeyId);
        }

        _customQueryHotkeyIds.Clear();
        ShutdownHotkeySystem();
        Log.Info(ClassName, "HotKeyMapper shutdown");
    }
}
