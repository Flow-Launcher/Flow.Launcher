using System;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

internal static partial class HotKeyMapper
{
    // Global hotkeys use Carbon RegisterEventHotKey on macOS; other platforms have no global hotkey backend.
    static partial void InitializeHotkeySystem()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacGlobalHotkey.Initialize();
        }
        else
        {
            Log.Warn(ClassName, "Global hotkeys are not supported on this platform");
        }
    }

    static partial void UnregisterHotkey(int hotkeyId)
    {
        if (OperatingSystem.IsMacOS() && !MacGlobalHotkey.Unregister(hotkeyId))
        {
            throw new InvalidOperationException($"UnregisterEventHotKey failed for hotkey id {hotkeyId}");
        }
    }

    static partial void ShutdownHotkeySystem()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacGlobalHotkey.Shutdown();
        }
    }

    private static partial bool IsForegroundWindowFullscreen() =>
        OperatingSystem.IsMacOS() && MacScreens.IsFrontmostWindowFullscreen();

    internal static partial bool CheckAvailability(HotkeyModel hotkey)
    {
        if (!TryGetRegistrationParts(hotkey, out var keyCode, out var modifiers))
        {
            return false;
        }

        // Try to register and immediately unregister
        var id = MacGlobalHotkey.Register(keyCode, modifiers, () => { });
        if (id >= 0)
        {
            MacGlobalHotkey.Unregister(id);
            return true;
        }

        return false;
    }

    private static partial bool TryRegisterHotkey(string hotkeyString, Action callback, out int hotkeyId)
    {
        hotkeyId = -1;

        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        if (!TryGetRegistrationParts(new HotkeyModel(hotkeyString), out var keyCode, out var modifiers))
        {
            Log.Error(ClassName, $"Failed to parse hotkey: {hotkeyString}");
            return false;
        }

        hotkeyId = MacGlobalHotkey.Register(keyCode, modifiers, callback);

        if (hotkeyId < 0)
        {
            Log.Error(ClassName, $"Failed to register hotkey: {hotkeyString}");
            return false;
        }

        return true;
    }

    private static bool TryGetRegistrationParts(HotkeyModel hotkey, out ushort keyCode, out uint modifiers)
    {
        keyCode = 0;
        modifiers = 0;

        if (!OperatingSystem.IsMacOS() || !hotkey.Validate(true))
        {
            return false;
        }

        modifiers = MacKeyMap.ToCarbonModifiers(hotkey);
        return MacKeyMap.TryGetKeyCode(hotkey.CharKey, out keyCode);
    }
}
