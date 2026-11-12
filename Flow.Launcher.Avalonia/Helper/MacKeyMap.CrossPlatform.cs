using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Flow.Launcher.Infrastructure.Hotkey;
using AvaloniaPhysicalKey = Avalonia.Input.PhysicalKey;
using Key = System.Windows.Input.Key;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Maps between the persisted hotkey model (WPF <see cref="Key"/> names), Avalonia physical keys and
/// macOS virtual key codes (kVK_*). Letters follow the active keyboard layout, like Windows virtual keys do;
/// every other key is matched by its physical (ANSI) position.
/// </summary>
internal static class MacKeyMap
{
    // Carbon modifier masks (Events.h).
    internal const uint CmdKey = 1 << 8;
    internal const uint ShiftKey = 1 << 9;
    internal const uint OptionKey = 1 << 11;
    internal const uint ControlKey = 1 << 12;

    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // Physical key, the key it is stored as, macOS virtual key code. The first row for a Key wins when registering.
    private static readonly (AvaloniaPhysicalKey Physical, Key Key, ushort KeyCode)[] KeyTable =
    {
        (AvaloniaPhysicalKey.A, Key.A, 0x00), (AvaloniaPhysicalKey.S, Key.S, 0x01), (AvaloniaPhysicalKey.D, Key.D, 0x02),
        (AvaloniaPhysicalKey.F, Key.F, 0x03), (AvaloniaPhysicalKey.H, Key.H, 0x04), (AvaloniaPhysicalKey.G, Key.G, 0x05),
        (AvaloniaPhysicalKey.Z, Key.Z, 0x06), (AvaloniaPhysicalKey.X, Key.X, 0x07), (AvaloniaPhysicalKey.C, Key.C, 0x08),
        (AvaloniaPhysicalKey.V, Key.V, 0x09), (AvaloniaPhysicalKey.B, Key.B, 0x0B), (AvaloniaPhysicalKey.Q, Key.Q, 0x0C),
        (AvaloniaPhysicalKey.W, Key.W, 0x0D), (AvaloniaPhysicalKey.E, Key.E, 0x0E), (AvaloniaPhysicalKey.R, Key.R, 0x0F),
        (AvaloniaPhysicalKey.Y, Key.Y, 0x10), (AvaloniaPhysicalKey.T, Key.T, 0x11), (AvaloniaPhysicalKey.O, Key.O, 0x1F),
        (AvaloniaPhysicalKey.U, Key.U, 0x20), (AvaloniaPhysicalKey.I, Key.I, 0x22), (AvaloniaPhysicalKey.P, Key.P, 0x23),
        (AvaloniaPhysicalKey.L, Key.L, 0x25), (AvaloniaPhysicalKey.J, Key.J, 0x26), (AvaloniaPhysicalKey.K, Key.K, 0x28),
        (AvaloniaPhysicalKey.N, Key.N, 0x2D), (AvaloniaPhysicalKey.M, Key.M, 0x2E),

        (AvaloniaPhysicalKey.Digit1, Key.D1, 0x12), (AvaloniaPhysicalKey.Digit2, Key.D2, 0x13),
        (AvaloniaPhysicalKey.Digit3, Key.D3, 0x14), (AvaloniaPhysicalKey.Digit4, Key.D4, 0x15),
        (AvaloniaPhysicalKey.Digit6, Key.D6, 0x16), (AvaloniaPhysicalKey.Digit5, Key.D5, 0x17),
        (AvaloniaPhysicalKey.Digit9, Key.D9, 0x19), (AvaloniaPhysicalKey.Digit7, Key.D7, 0x1A),
        (AvaloniaPhysicalKey.Digit8, Key.D8, 0x1C), (AvaloniaPhysicalKey.Digit0, Key.D0, 0x1D),

        (AvaloniaPhysicalKey.Equal, Key.OemPlus, 0x18), (AvaloniaPhysicalKey.Minus, Key.OemMinus, 0x1B),
        (AvaloniaPhysicalKey.BracketRight, Key.Oem6, 0x1E), (AvaloniaPhysicalKey.BracketLeft, Key.Oem4, 0x21),
        (AvaloniaPhysicalKey.Quote, Key.Oem7, 0x27), (AvaloniaPhysicalKey.Semicolon, Key.Oem1, 0x29),
        (AvaloniaPhysicalKey.Backslash, Key.Oem5, 0x2A), (AvaloniaPhysicalKey.Comma, Key.OemComma, 0x2B),
        (AvaloniaPhysicalKey.Slash, Key.Oem2, 0x2C), (AvaloniaPhysicalKey.Period, Key.OemPeriod, 0x2F),
        (AvaloniaPhysicalKey.Backquote, Key.Oem3, 0x32), (AvaloniaPhysicalKey.IntlBackslash, Key.Oem102, 0x0A),
        (AvaloniaPhysicalKey.IntlYen, Key.Oem8, 0x5D), (AvaloniaPhysicalKey.IntlRo, Key.AbntC1, 0x5E),

        (AvaloniaPhysicalKey.Enter, Key.Return, 0x24), (AvaloniaPhysicalKey.Tab, Key.Tab, 0x30),
        (AvaloniaPhysicalKey.Space, Key.Space, 0x31), (AvaloniaPhysicalKey.Backspace, Key.Back, 0x33),
        (AvaloniaPhysicalKey.Escape, Key.Escape, 0x35), (AvaloniaPhysicalKey.CapsLock, Key.CapsLock, 0x39),
        (AvaloniaPhysicalKey.Lang2, Key.HanjaMode, 0x66), (AvaloniaPhysicalKey.Lang1, Key.KanaMode, 0x68),
        (AvaloniaPhysicalKey.ContextMenu, Key.Apps, 0x6E),

        (AvaloniaPhysicalKey.MetaRight, Key.RWin, 0x36), (AvaloniaPhysicalKey.MetaLeft, Key.LWin, 0x37),
        (AvaloniaPhysicalKey.ShiftLeft, Key.LeftShift, 0x38), (AvaloniaPhysicalKey.AltLeft, Key.LeftAlt, 0x3A),
        (AvaloniaPhysicalKey.ControlLeft, Key.LeftCtrl, 0x3B), (AvaloniaPhysicalKey.ShiftRight, Key.RightShift, 0x3C),
        (AvaloniaPhysicalKey.AltRight, Key.RightAlt, 0x3D), (AvaloniaPhysicalKey.ControlRight, Key.RightCtrl, 0x3E),

        (AvaloniaPhysicalKey.Insert, Key.Insert, 0x72), (AvaloniaPhysicalKey.Help, Key.Help, 0x72),
        (AvaloniaPhysicalKey.Home, Key.Home, 0x73), (AvaloniaPhysicalKey.PageUp, Key.PageUp, 0x74),
        (AvaloniaPhysicalKey.Delete, Key.Delete, 0x75), (AvaloniaPhysicalKey.End, Key.End, 0x77),
        (AvaloniaPhysicalKey.PageDown, Key.PageDown, 0x79), (AvaloniaPhysicalKey.ArrowLeft, Key.Left, 0x7B),
        (AvaloniaPhysicalKey.ArrowRight, Key.Right, 0x7C), (AvaloniaPhysicalKey.ArrowDown, Key.Down, 0x7D),
        (AvaloniaPhysicalKey.ArrowUp, Key.Up, 0x7E),

        (AvaloniaPhysicalKey.NumPadDecimal, Key.Decimal, 0x41), (AvaloniaPhysicalKey.NumPadMultiply, Key.Multiply, 0x43),
        (AvaloniaPhysicalKey.NumPadAdd, Key.Add, 0x45), (AvaloniaPhysicalKey.NumLock, Key.Clear, 0x47),
        (AvaloniaPhysicalKey.NumPadClear, Key.Clear, 0x47), (AvaloniaPhysicalKey.NumPadDivide, Key.Divide, 0x4B),
        (AvaloniaPhysicalKey.NumPadEnter, Key.Return, 0x4C), (AvaloniaPhysicalKey.NumPadSubtract, Key.Subtract, 0x4E),
        (AvaloniaPhysicalKey.NumPadEqual, Key.OemPlus, 0x51), (AvaloniaPhysicalKey.NumPadComma, Key.Separator, 0x5F),
        (AvaloniaPhysicalKey.NumPad0, Key.NumPad0, 0x52), (AvaloniaPhysicalKey.NumPad1, Key.NumPad1, 0x53),
        (AvaloniaPhysicalKey.NumPad2, Key.NumPad2, 0x54), (AvaloniaPhysicalKey.NumPad3, Key.NumPad3, 0x55),
        (AvaloniaPhysicalKey.NumPad4, Key.NumPad4, 0x56), (AvaloniaPhysicalKey.NumPad5, Key.NumPad5, 0x57),
        (AvaloniaPhysicalKey.NumPad6, Key.NumPad6, 0x58), (AvaloniaPhysicalKey.NumPad7, Key.NumPad7, 0x59),
        (AvaloniaPhysicalKey.NumPad8, Key.NumPad8, 0x5B), (AvaloniaPhysicalKey.NumPad9, Key.NumPad9, 0x5C),

        (AvaloniaPhysicalKey.F1, Key.F1, 0x7A), (AvaloniaPhysicalKey.F2, Key.F2, 0x78), (AvaloniaPhysicalKey.F3, Key.F3, 0x63),
        (AvaloniaPhysicalKey.F4, Key.F4, 0x76), (AvaloniaPhysicalKey.F5, Key.F5, 0x60), (AvaloniaPhysicalKey.F6, Key.F6, 0x61),
        (AvaloniaPhysicalKey.F7, Key.F7, 0x62), (AvaloniaPhysicalKey.F8, Key.F8, 0x64), (AvaloniaPhysicalKey.F9, Key.F9, 0x65),
        (AvaloniaPhysicalKey.F10, Key.F10, 0x6D), (AvaloniaPhysicalKey.F11, Key.F11, 0x67), (AvaloniaPhysicalKey.F12, Key.F12, 0x6F),
        (AvaloniaPhysicalKey.F13, Key.F13, 0x69), (AvaloniaPhysicalKey.F14, Key.F14, 0x6B), (AvaloniaPhysicalKey.F15, Key.F15, 0x71),
        (AvaloniaPhysicalKey.F16, Key.F16, 0x6A), (AvaloniaPhysicalKey.F17, Key.F17, 0x40), (AvaloniaPhysicalKey.F18, Key.F18, 0x4F),
        (AvaloniaPhysicalKey.F19, Key.F19, 0x50), (AvaloniaPhysicalKey.F20, Key.F20, 0x5A),

        (AvaloniaPhysicalKey.AudioVolumeUp, Key.VolumeUp, 0x48), (AvaloniaPhysicalKey.AudioVolumeDown, Key.VolumeDown, 0x49),
        (AvaloniaPhysicalKey.AudioVolumeMute, Key.VolumeMute, 0x4A),
    };

    private static readonly Dictionary<Key, ushort> KeyCodeByKey = BuildKeyCodeByKey();

    private static Dictionary<Key, ushort> BuildKeyCodeByKey()
    {
        var map = new Dictionary<Key, ushort>();
        foreach (var (_, key, keyCode) in KeyTable)
        {
            map.TryAdd(key, keyCode);
        }

        return map;
    }

    internal static uint ToCarbonModifiers(HotkeyModel hotkey)
    {
        uint modifiers = 0;
        if (hotkey.Win) modifiers |= CmdKey;
        if (hotkey.Alt) modifiers |= OptionKey;
        if (hotkey.Ctrl) modifiers |= ControlKey;
        if (hotkey.Shift) modifiers |= ShiftKey;
        return modifiers;
    }

    /// <summary>
    /// Resolves the macOS virtual key code for a stored key. Letters are looked up in the active keyboard layout.
    /// </summary>
    internal static bool TryGetKeyCode(Key key, out ushort keyCode)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            using var layout = KeyboardLayout.Current();
            var letter = (char)('a' + (key - Key.A));
            foreach (var (_, _, candidate) in KeyTable)
            {
                if (layout.TryGetLetter(candidate, out var produced) && produced == letter)
                {
                    keyCode = candidate;
                    return true;
                }
            }
        }

        return KeyCodeByKey.TryGetValue(key, out keyCode);
    }

    /// <summary>
    /// Resolves the stored key for a physical key press. Keys producing a letter in the active layout map to that letter.
    /// </summary>
    internal static Key GetKey(AvaloniaPhysicalKey physicalKey)
    {
        foreach (var (physical, key, keyCode) in KeyTable)
        {
            if (physical != physicalKey)
            {
                continue;
            }

            using var layout = KeyboardLayout.Current();
            return layout.TryGetLetter(keyCode, out var letter) ? Key.A + (letter - 'a') : key;
        }

        return Key.None;
    }

    /// <summary>
    /// Builds a lookup from macOS virtual key code (index) to stored key: every key maps to the first table row with its
    /// code and, when <paramref name="followLayout"/> is set, keys producing a letter in the active layout map to that
    /// letter. Text Input Sources must be queried on the main thread, so only follow the layout there.
    /// </summary>
    internal static Key[] CreateKeyCodeTable(bool followLayout)
    {
        var table = new Key[128];
        foreach (var (_, key, keyCode) in KeyTable)
        {
            if (keyCode < table.Length && table[keyCode] == Key.None)
            {
                table[keyCode] = key;
            }
        }

        if (!followLayout)
        {
            return table;
        }

        using var layout = KeyboardLayout.Current();
        for (ushort keyCode = 0; keyCode < table.Length; keyCode++)
        {
            if (table[keyCode] != Key.None && layout.TryGetLetter(keyCode, out var letter))
            {
                table[keyCode] = Key.A + (letter - 'a');
            }
        }

        return table;
    }

    /// <summary>
    /// The active keyboard layout (for input methods, the ASCII-capable layout they type with).
    /// </summary>
    private readonly struct KeyboardLayout : IDisposable
    {
        private readonly IntPtr _source;
        private readonly IntPtr _layout;
        private readonly uint _keyboardType;

        private KeyboardLayout(IntPtr source, IntPtr layout, uint keyboardType)
        {
            _source = source;
            _layout = layout;
            _keyboardType = keyboardType;
        }

        internal static KeyboardLayout Current()
        {
            if (!OperatingSystem.IsMacOS())
            {
                return default;
            }

            var source = TISCopyCurrentKeyboardLayoutInputSource();
            if (source == IntPtr.Zero)
            {
                return default;
            }

            var layoutData = TISGetInputSourceProperty(source, UnicodeKeyLayoutDataProperty.Value);
            var layout = layoutData == IntPtr.Zero ? IntPtr.Zero : CFDataGetBytePtr(layoutData);
            return new KeyboardLayout(source, layout, LMGetKbdType());
        }

        internal bool TryGetLetter(ushort keyCode, out char letter)
        {
            letter = '\0';
            if (_layout == IntPtr.Zero)
            {
                return false;
            }

            uint deadKeyState = 0;
            var status = UCKeyTranslate(_layout, keyCode, KUCKeyActionDisplay, 0, _keyboardType,
                KUCKeyTranslateNoDeadKeysMask, ref deadKeyState, 1, out var length, out var produced);
            if (status != 0 || length != 1)
            {
                return false;
            }

            var c = char.ToLowerInvariant((char)produced);
            if (c is < 'a' or > 'z')
            {
                return false;
            }

            letter = c;
            return true;
        }

        public void Dispose()
        {
            if (_source != IntPtr.Zero)
            {
                CFRelease(_source);
            }
        }
    }

    private const ushort KUCKeyActionDisplay = 3;
    private const uint KUCKeyTranslateNoDeadKeysMask = 1;

    private static readonly Lazy<IntPtr> UnicodeKeyLayoutDataProperty = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(Carbon), "kTISPropertyUnicodeKeyLayoutData")));

    [DllImport(Carbon)]
    private static extern IntPtr TISCopyCurrentKeyboardLayoutInputSource();

    [DllImport(Carbon)]
    private static extern IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);

    [DllImport(Carbon)]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(Carbon)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(Carbon)]
    private static extern byte LMGetKbdType();

    [DllImport(Carbon)]
    private static extern int UCKeyTranslate(IntPtr keyLayout, ushort virtualKeyCode, ushort keyAction,
        uint modifierKeyState, uint keyboardType, uint keyTranslateOptions, ref uint deadKeyState,
        nuint maxStringLength, out nuint actualStringLength, out ushort unicodeString);
}
