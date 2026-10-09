using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// System-wide hotkeys on macOS through Carbon RegisterEventHotKey. Needs no Accessibility permission; the system
/// delivers kEventHotKeyPressed to the application event target on the main thread.
/// </summary>
internal static class MacGlobalHotkey
{
    private static readonly string ClassName = nameof(MacGlobalHotkey);

    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    private const int NoErr = 0;
    private const int EventNotHandledErr = -9874;
    private const uint KEventHotKeyExclusive = 1;

    private static readonly uint HotKeySignature = FourCharCode("FLOW");
    private static readonly uint KEventClassKeyboard = FourCharCode("keyb");
    private const uint KEventHotKeyPressed = 5;
    private static readonly uint KEventParamDirectObject = FourCharCode("----");
    private static readonly uint TypeEventHotKeyID = FourCharCode("hkid");

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint Signature;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventHandlerProc(IntPtr nextHandler, IntPtr theEvent, IntPtr userData);

    // Kept alive for the lifetime of the installed handler.
    private static readonly EventHandlerProc HandlerProc = OnHotKeyEvent;

    private static readonly Dictionary<uint, (IntPtr HotKeyRef, Action Callback)> Hotkeys = new();
    private static IntPtr _handlerRef;
    private static uint _nextId = 1;

    internal static void Initialize()
    {
        if (_handlerRef != IntPtr.Zero)
        {
            return;
        }

        var eventType = new EventTypeSpec { EventClass = KEventClassKeyboard, EventKind = KEventHotKeyPressed };
        var status = InstallEventHandler(GetApplicationEventTarget(), Marshal.GetFunctionPointerForDelegate(HandlerProc),
            1, new[] { eventType }, IntPtr.Zero, out _handlerRef);

        if (status != NoErr)
        {
            _handlerRef = IntPtr.Zero;
            Log.Error(ClassName, $"InstallEventHandler failed (OSStatus {status})");
            return;
        }

        Log.Info(ClassName, "Carbon hotkey handler installed");
    }

    /// <summary>
    /// Registers a hotkey. Returns the hotkey id, or -1 when the combination is taken or registration failed.
    /// </summary>
    internal static int Register(ushort keyCode, uint carbonModifiers, Action callback)
    {
        if (_handlerRef == IntPtr.Zero)
        {
            Log.Warn(ClassName, "Not initialized");
            return -1;
        }

        if (IsSystemHotkey(keyCode, carbonModifiers))
        {
            Log.Warn(ClassName, $"Hotkey key=0x{keyCode:X2}, mods=0x{carbonModifiers:X} is reserved by a macOS keyboard shortcut");
            return -1;
        }

        var id = _nextId++;
        var hotKeyId = new EventHotKeyID { Signature = HotKeySignature, Id = id };
        var status = RegisterEventHotKey(keyCode, carbonModifiers, hotKeyId, GetApplicationEventTarget(),
            KEventHotKeyExclusive, out var hotKeyRef);

        if (status != NoErr)
        {
            Log.Error(ClassName, $"RegisterEventHotKey failed (OSStatus {status}) for key=0x{keyCode:X2}, mods=0x{carbonModifiers:X}");
            return -1;
        }

        Hotkeys[id] = (hotKeyRef, callback);
        Log.Info(ClassName, $"RegisterEventHotKey returned noErr: id={id}, key=0x{keyCode:X2}, mods=0x{carbonModifiers:X}");
        return (int)id;
    }

    internal static void Unregister(int hotkeyId)
    {
        if (hotkeyId < 0 || !Hotkeys.Remove((uint)hotkeyId, out var hotkey))
        {
            return;
        }

        var status = UnregisterEventHotKey(hotkey.HotKeyRef);
        if (status != NoErr)
        {
            Log.Warn(ClassName, $"UnregisterEventHotKey failed (OSStatus {status}) for id={hotkeyId}");
        }
    }

    internal static void Shutdown()
    {
        foreach (var (hotKeyRef, _) in Hotkeys.Values)
        {
            UnregisterEventHotKey(hotKeyRef);
        }

        Hotkeys.Clear();

        if (_handlerRef != IntPtr.Zero)
        {
            RemoveEventHandler(_handlerRef);
            _handlerRef = IntPtr.Zero;
        }
    }

    private static int OnHotKeyEvent(IntPtr nextHandler, IntPtr theEvent, IntPtr userData)
    {
        try
        {
            var status = GetEventParameter(theEvent, KEventParamDirectObject, TypeEventHotKeyID, IntPtr.Zero,
                (nuint)Marshal.SizeOf<EventHotKeyID>(), IntPtr.Zero, out var hotKeyId);

            if (status != NoErr || hotKeyId.Signature != HotKeySignature
                || !Hotkeys.TryGetValue(hotKeyId.Id, out var hotkey))
            {
                return EventNotHandledErr;
            }

            Log.Debug(ClassName, $"Hotkey triggered id={hotKeyId.Id}");
            Dispatcher.UIThread.Post(hotkey.Callback);
            return NoErr;
        }
        catch (Exception e)
        {
            // Exceptions must not unwind into the Carbon event dispatcher.
            Log.Exception(ClassName, "Hotkey event handler failed", e);
            return EventNotHandledErr;
        }
    }

    /// <summary>
    /// Whether the combination is an enabled macOS keyboard shortcut (System Settings > Keyboard > Keyboard Shortcuts),
    /// e.g. Cmd+Space for Spotlight. Such hotkeys register successfully but never reach the application.
    /// </summary>
    private static bool IsSystemHotkey(ushort keyCode, uint carbonModifiers)
    {
        if (CopySymbolicHotKeys(out var hotkeys) != NoErr || hotkeys == IntPtr.Zero)
        {
            return false;
        }

        var codeKey = CreateCFString("kHISymbolicHotKeyCode");
        var modifiersKey = CreateCFString("kHISymbolicHotKeyModifiers");
        var enabledKey = CreateCFString("kHISymbolicHotKeyEnabled");
        try
        {
            var count = CFArrayGetCount(hotkeys);
            for (nint i = 0; i < count; i++)
            {
                var entry = CFArrayGetValueAtIndex(hotkeys, i);
                var enabled = CFDictionaryGetValue(entry, enabledKey);
                if (enabled == IntPtr.Zero || CFBooleanGetValue(enabled) == 0)
                {
                    continue;
                }

                if (TryGetNumber(entry, codeKey, out var code) && code == keyCode
                    && TryGetNumber(entry, modifiersKey, out var modifiers)
                    && ((uint)modifiers & RelevantModifiers) == carbonModifiers)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            CFRelease(codeKey);
            CFRelease(modifiersKey);
            CFRelease(enabledKey);
            CFRelease(hotkeys);
        }
    }

    private const uint RelevantModifiers = MacKeyMap.CmdKey | MacKeyMap.ShiftKey | MacKeyMap.OptionKey | MacKeyMap.ControlKey;

    private static bool TryGetNumber(IntPtr dictionary, IntPtr key, out long value)
    {
        value = 0;
        var number = CFDictionaryGetValue(dictionary, key);
        return number != IntPtr.Zero && CFNumberGetValue(number, KCFNumberSInt64Type, out value) != 0;
    }

    private static IntPtr CreateCFString(string value) => CFStringCreateWithCString(IntPtr.Zero, value, KCFStringEncodingUTF8);

    private static uint FourCharCode(string code) =>
        (uint)code[0] << 24 | (uint)code[1] << 16 | (uint)code[2] << 8 | code[3];

    private const int KCFNumberSInt64Type = 4;
    private const uint KCFStringEncodingUTF8 = 0x08000100;

    [DllImport(Carbon)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(Carbon)]
    private static extern int InstallEventHandler(IntPtr target, IntPtr handler, nuint numTypes,
        [In] EventTypeSpec[] typeList, IntPtr userData, out IntPtr handlerRef);

    [DllImport(Carbon)]
    private static extern int RemoveEventHandler(IntPtr handlerRef);

    [DllImport(Carbon)]
    private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyID hotKeyId, IntPtr target,
        uint options, out IntPtr hotKeyRef);

    [DllImport(Carbon)]
    private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);

    [DllImport(Carbon)]
    private static extern int GetEventParameter(IntPtr theEvent, uint name, uint desiredType, IntPtr actualType,
        nuint bufferSize, IntPtr actualSize, out EventHotKeyID data);

    [DllImport(Carbon)]
    private static extern int CopySymbolicHotKeys(out IntPtr hotKeyArray);

    [DllImport(Carbon)]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(Carbon)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(Carbon)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(Carbon)]
    private static extern byte CFBooleanGetValue(IntPtr boolean);

    [DllImport(Carbon)]
    private static extern byte CFNumberGetValue(IntPtr number, int type, out long value);

    [DllImport(Carbon, CharSet = CharSet.Ansi)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    [DllImport(Carbon)]
    private static extern void CFRelease(IntPtr cf);
}
