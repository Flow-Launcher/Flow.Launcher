using System;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using System.Windows.Input;

namespace Flow.Launcher.Avalonia.Helper;

internal static partial class HotKeyMapper
{
    static partial void InitializeHotkeySystem()
    {
        GlobalHotkey.Initialize();
    }

    static partial void UnregisterHotkey(int hotkeyId)
    {
        GlobalHotkey.Unregister(hotkeyId);
    }

    static partial void ShutdownHotkeySystem()
    {
        GlobalHotkey.Shutdown();
    }

    private static partial bool IsForegroundWindowFullscreen()
    {
        return Win32Helper.IsForegroundWindowFullscreen();
    }

    internal static partial bool CheckAvailability(HotkeyModel hotkey)
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

    private static partial bool TryRegisterHotkey(string hotkeyString, Action callback, out int hotkeyId)
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
}
