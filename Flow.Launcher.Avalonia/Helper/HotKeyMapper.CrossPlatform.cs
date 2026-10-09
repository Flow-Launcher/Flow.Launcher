using System;
using Flow.Launcher.Infrastructure.Hotkey;

namespace Flow.Launcher.Avalonia.Helper;

internal static partial class HotKeyMapper
{
    // Fullscreen detection is Win32-only.
    private static partial bool IsForegroundWindowFullscreen() => false;

    // Global hotkey registration is Win32-only; no hotkey is ever available here.
    internal static partial bool CheckAvailability(HotkeyModel hotkey) => false;

    // Global hotkey registration is Win32-only; registration always fails here.
    private static partial bool TryRegisterHotkey(string hotkeyString, Action callback, out int hotkeyId)
    {
        hotkeyId = -1;
        return false;
    }
}
