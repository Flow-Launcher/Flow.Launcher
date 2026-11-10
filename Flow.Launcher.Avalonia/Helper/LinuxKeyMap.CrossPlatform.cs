using System.Collections.Generic;
using Flow.Launcher.Infrastructure.Hotkey;
using Key = System.Windows.Input.Key;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Maps the persisted hotkey model (WPF <see cref="Key"/> names) to X11 keysyms. The keysym name doubles as the key
/// part of an XDG shortcut trigger ("CTRL+ALT+space"), which is what the GlobalShortcuts portal expects.
/// </summary>
internal static class LinuxKeyMap
{
    // X11 core modifier masks (X.h).
    internal const ushort ShiftMask = 1 << 0;
    internal const ushort LockMask = 1 << 1;
    internal const ushort ControlMask = 1 << 2;
    internal const ushort Mod1Mask = 1 << 3; // Alt
    internal const ushort Mod2Mask = 1 << 4; // Num Lock on practically every keymap
    internal const ushort Mod4Mask = 1 << 6; // Super

    internal const ushort RelevantModifiers = ShiftMask | ControlMask | Mod1Mask | Mod4Mask;

    private static readonly Dictionary<Key, (uint Keysym, string Name)> Keysyms = BuildKeysyms();

    private static Dictionary<Key, (uint, string)> BuildKeysyms()
    {
        var map = new Dictionary<Key, (uint, string)>();

        for (var i = 0; i < 26; i++)
        {
            var letter = (char)('a' + i);
            map[Key.A + i] = (letter, letter.ToString());
        }

        for (var i = 0; i < 10; i++)
        {
            var digit = (char)('0' + i);
            map[Key.D0 + i] = (digit, digit.ToString());
            map[Key.NumPad0 + i] = (0xffb0u + (uint)i, $"KP_{i}");
        }

        for (var i = 0; i < 24; i++)
        {
            map[Key.F1 + i] = (0xffbeu + (uint)i, $"F{i + 1}");
        }

        void Add(Key key, uint keysym, string name) => map[key] = (keysym, name);

        Add(Key.Space, 0x0020, "space");
        Add(Key.Return, 0xff0d, "Return");
        Add(Key.Tab, 0xff09, "Tab");
        Add(Key.Escape, 0xff1b, "Escape");
        Add(Key.Back, 0xff08, "BackSpace");
        Add(Key.Delete, 0xffff, "Delete");
        Add(Key.Insert, 0xff63, "Insert");
        Add(Key.Home, 0xff50, "Home");
        Add(Key.End, 0xff57, "End");
        Add(Key.PageUp, 0xff55, "Prior");
        Add(Key.PageDown, 0xff56, "Next");
        Add(Key.Left, 0xff51, "Left");
        Add(Key.Up, 0xff52, "Up");
        Add(Key.Right, 0xff53, "Right");
        Add(Key.Down, 0xff54, "Down");
        Add(Key.Pause, 0xff13, "Pause");
        Add(Key.Scroll, 0xff14, "Scroll_Lock");
        Add(Key.PrintScreen, 0xff61, "Print");
        Add(Key.Apps, 0xff67, "Menu");
        Add(Key.CapsLock, 0xffe5, "Caps_Lock");
        Add(Key.NumLock, 0xff7f, "Num_Lock");

        Add(Key.Multiply, 0xffaa, "KP_Multiply");
        Add(Key.Add, 0xffab, "KP_Add");
        Add(Key.Separator, 0xffac, "KP_Separator");
        Add(Key.Subtract, 0xffad, "KP_Subtract");
        Add(Key.Decimal, 0xffae, "KP_Decimal");
        Add(Key.Divide, 0xffaf, "KP_Divide");

        // OEM keys by their US-layout symbol, like the Windows virtual keys they are stored as.
        Add(Key.OemPlus, 0x003d, "equal");
        Add(Key.OemMinus, 0x002d, "minus");
        Add(Key.Oem4, 0x005b, "bracketleft");
        Add(Key.Oem6, 0x005d, "bracketright");
        Add(Key.Oem7, 0x0027, "apostrophe");
        Add(Key.Oem1, 0x003b, "semicolon");
        Add(Key.Oem5, 0x005c, "backslash");
        Add(Key.OemComma, 0x002c, "comma");
        Add(Key.Oem2, 0x002f, "slash");
        Add(Key.OemPeriod, 0x002e, "period");
        Add(Key.Oem3, 0x0060, "grave");
        Add(Key.Oem102, 0x003c, "less");

        Add(Key.VolumeUp, 0x1008ff13, "XF86AudioRaiseVolume");
        Add(Key.VolumeDown, 0x1008ff11, "XF86AudioLowerVolume");
        Add(Key.VolumeMute, 0x1008ff12, "XF86AudioMute");
        Add(Key.MediaPlayPause, 0x1008ff14, "XF86AudioPlay");
        Add(Key.MediaStop, 0x1008ff15, "XF86AudioStop");
        Add(Key.MediaPreviousTrack, 0x1008ff16, "XF86AudioPrev");
        Add(Key.MediaNextTrack, 0x1008ff17, "XF86AudioNext");
        Add(Key.BrowserHome, 0x1008ff18, "XF86HomePage");
        Add(Key.LaunchMail, 0x1008ff19, "XF86Mail");

        return map;
    }

    internal static bool TryGetKeysym(Key key, out uint keysym, out string name)
    {
        if (Keysyms.TryGetValue(key, out var entry))
        {
            (keysym, name) = entry;
            return true;
        }

        keysym = 0;
        name = string.Empty;
        return false;
    }

    internal static ushort ToX11Modifiers(HotkeyModel hotkey)
    {
        ushort modifiers = 0;
        if (hotkey.Shift) modifiers |= ShiftMask;
        if (hotkey.Ctrl) modifiers |= ControlMask;
        if (hotkey.Alt) modifiers |= Mod1Mask;
        if (hotkey.Win) modifiers |= Mod4Mask;
        return modifiers;
    }

    /// <summary>
    /// The XDG shortcut trigger for a hotkey, e.g. "CTRL+ALT+space" (modifier names from the xdg-desktop-portal
    /// shortcuts specification, key names are xkb keysym names).
    /// </summary>
    internal static string ToTrigger(HotkeyModel hotkey, string keysymName)
    {
        var parts = new List<string>(5);
        if (hotkey.Ctrl) parts.Add("CTRL");
        if (hotkey.Alt) parts.Add("ALT");
        if (hotkey.Shift) parts.Add("SHIFT");
        if (hotkey.Win) parts.Add("LOGO");
        parts.Add(keysymName);
        return string.Join('+', parts);
    }
}
