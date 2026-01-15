using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

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

        Log.Info(ClassName, $"HotKeyMapper initialized with hotkey: {_settings.Hotkey}");
    }

    /// <summary>
    /// Set or update the toggle hotkey.
    /// </summary>
    internal static void SetToggleHotkey(string hotkeyString)
    {
        // Unregister existing hotkey
        if (_toggleHotkeyId >= 0)
        {
            GlobalHotkey.Unregister(_toggleHotkeyId);
            _toggleHotkeyId = -1;
        }

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

    private static void OnToggleHotkey()
    {
        Log.Info(ClassName, "Toggle hotkey triggered");
        _mainViewModel?.ToggleFlowLauncher();
    }

    /// <summary>
    /// Cleanup and unregister all hotkeys.
    /// </summary>
    internal static void Shutdown()
    {
        GlobalHotkey.Shutdown();
        Log.Info(ClassName, "HotKeyMapper shutdown");
    }
}
