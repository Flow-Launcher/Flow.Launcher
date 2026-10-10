using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;
using Key = System.Windows.Input.Key;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// System-wide keyboard listener on macOS, the counterpart of the Windows WH_KEYBOARD_LL hook: a Quartz event tap on a
/// background run loop reports key presses as Windows key events and virtual key codes. Blocking keys needs the
/// Accessibility permission; without it the tap only listens (which still needs Input Monitoring).
/// The callback runs on the tap thread and must return quickly.
/// </summary>
public static class MacKeyboardHook
{
    private static readonly string ClassName = nameof(MacKeyboardHook);

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // CGEventTapLocation / CGEventTapPlacement / CGEventTapOptions
    private const uint SessionEventTap = 1;
    private const uint HeadInsertEventTap = 0;
    private const uint TapOptionDefault = 0;
    private const uint TapOptionListenOnly = 1;

    // CGEventType
    private const uint KeyDownEvent = 10;
    private const uint KeyUpEvent = 11;
    private const uint FlagsChangedEvent = 12;
    private const uint TapDisabledByTimeout = 0xFFFFFFFE;
    private const uint TapDisabledByUserInput = 0xFFFFFFFF;

    private const ulong EventMask = (1UL << (int)KeyDownEvent) | (1UL << (int)KeyUpEvent) | (1UL << (int)FlagsChangedEvent);

    // CGEventField
    private const uint KeyboardEventKeycode = 9;

    // CGEventFlags
    private const ulong ShiftFlag = 0x20000;
    private const ulong ControlFlag = 0x40000;
    private const ulong AlternateFlag = 0x80000;
    private const ulong CommandFlag = 0x100000;

    // Device-dependent modifier bits (IOLLEvent.h), distinguishing left and right modifier keys.
    private const ulong DeviceLeftControl = 0x0001;
    private const ulong DeviceLeftShift = 0x0002;
    private const ulong DeviceRightShift = 0x0004;
    private const ulong DeviceLeftCommand = 0x0008;
    private const ulong DeviceRightCommand = 0x0010;
    private const ulong DeviceLeftAlternate = 0x0020;
    private const ulong DeviceRightAlternate = 0x0040;
    private const ulong DeviceRightControl = 0x2000;

    // macOS virtual key codes of modifier keys (kVK_*).
    private const ushort KeyCodeRightCommand = 0x36;
    private const ushort KeyCodeCommand = 0x37;
    private const ushort KeyCodeShift = 0x38;
    private const ushort KeyCodeCapsLock = 0x39;
    private const ushort KeyCodeOption = 0x3A;
    private const ushort KeyCodeControl = 0x3B;
    private const ushort KeyCodeRightShift = 0x3C;
    private const ushort KeyCodeRightOption = 0x3D;
    private const ushort KeyCodeRightControl = 0x3E;

    private const nint DeliverImmediately = 4; // CFNotificationSuspensionBehaviorDeliverImmediately

    private static readonly object SyncRoot = new();
    private static readonly CGEventTapCallBack TapCallback = OnTapEvent;
    private static readonly CFNotificationCallback InputSourceChangedCallback = OnInputSourceChanged;
    private static readonly IntPtr ObserverToken = Marshal.AllocHGlobal(1);

    private static Func<KeyEvent, int, SpecialKeyState, bool>? _callback;
    private static Thread? _thread;
    private static IntPtr _tap;
    private static IntPtr _runLoop;
    private static volatile bool _listenOnly;
    private static volatile bool _stopRequested;
    private static volatile Key[] _keyTable = [];
    private static bool _observingInputSource;

    /// <summary>
    /// Starts listening. Calling it again while running only replaces the callback. A callback returning false swallows
    /// the event, unless the tap is listen-only (no Accessibility permission).
    /// </summary>
    public static void Start(Func<KeyEvent, int, SpecialKeyState, bool> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (!OperatingSystem.IsMacOS())
        {
            Log.Warn(ClassName, "Global keyboard hook is not supported on this platform");
            return;
        }

        lock (SyncRoot)
        {
            _callback = callback;
            if (_thread != null)
            {
                return;
            }

            LoadKeyTable();
            ObserveInputSourceChanges();

            _stopRequested = false;
            // Not disposed: the tap thread signals it again when it exits.
            var ready = new ManualResetEventSlim();
            var thread = new Thread(() => RunTap(ready)) { IsBackground = true, Name = ClassName };
            thread.Start();
            ready.Wait();

            if (_tap == IntPtr.Zero)
            {
                // The tap could not be created or failed right away; the thread exits. A later Start retries.
                thread.Join();
                if (_runLoop != IntPtr.Zero)
                {
                    CFRelease(_runLoop);
                    _runLoop = IntPtr.Zero;
                }

                _callback = null;
                StopObservingInputSourceChanges();
                return;
            }

            _thread = thread;
        }
    }

    /// <summary>
    /// Stops listening and tears down the event tap.
    /// </summary>
    public static void Stop()
    {
        Thread? thread;
        lock (SyncRoot)
        {
            thread = _thread;
            if (thread == null)
            {
                return;
            }

            _thread = null;
            _callback = null;
            _stopRequested = true;
            CFRunLoopStop(_runLoop);
        }

        thread.Join();

        lock (SyncRoot)
        {
            CFRelease(_runLoop);
            _runLoop = IntPtr.Zero;
            StopObservingInputSourceChanges();
        }

        Log.Info(ClassName, "Keyboard event tap stopped");
    }

    private static void RunTap(ManualResetEventSlim ready)
    {
        var tap = IntPtr.Zero;
        var source = IntPtr.Zero;
        try
        {
            var trusted = AXIsProcessTrusted();
            if (!trusted)
            {
                Log.Warn(ClassName, "Accessibility permission is not granted; the keyboard hook can only listen and cannot block keys");
            }

            if (trusted)
            {
                tap = CGEventTapCreate(SessionEventTap, HeadInsertEventTap, TapOptionDefault, EventMask, TapCallback, IntPtr.Zero);
            }

            _listenOnly = tap == IntPtr.Zero;
            if (tap == IntPtr.Zero)
            {
                tap = CGEventTapCreate(SessionEventTap, HeadInsertEventTap, TapOptionListenOnly, EventMask, TapCallback, IntPtr.Zero);
            }

            if (tap == IntPtr.Zero)
            {
                Log.Error(ClassName, "CGEventTapCreate failed; grant Flow Launcher the Input Monitoring or Accessibility permission");
                return;
            }

            source = CFMachPortCreateRunLoopSource(IntPtr.Zero, tap, 0);
            if (source == IntPtr.Zero)
            {
                Log.Error(ClassName, "CFMachPortCreateRunLoopSource failed");
                CFMachPortInvalidate(tap);
                CFRelease(tap);
                tap = IntPtr.Zero;
                return;
            }

            var runLoop = CFRunLoopGetCurrent();
            var mode = DefaultRunLoopMode.Value;
            CFRunLoopAddSource(runLoop, source, mode);
            CGEventTapEnable(tap, true);

            _runLoop = CFRetain(runLoop);
            _tap = tap;
            Log.Info(ClassName, $"Keyboard event tap started ({(_listenOnly ? "listen-only" : "active")})");
            ready.Set();

            while (!_stopRequested)
            {
                CFRunLoopRunInMode(mode, 1.0, false);
            }

            CGEventTapEnable(tap, false);
            CFRunLoopRemoveSource(runLoop, source, mode);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Keyboard event tap failed", e);
        }
        finally
        {
            _tap = IntPtr.Zero;
            if (source != IntPtr.Zero)
            {
                CFRelease(source);
            }

            if (tap != IntPtr.Zero)
            {
                CFMachPortInvalidate(tap);
                CFRelease(tap);
            }

            ready.Set();
        }
    }

    private static IntPtr OnTapEvent(IntPtr proxy, uint type, IntPtr cgEvent, IntPtr userInfo)
    {
        try
        {
            if (type is TapDisabledByTimeout or TapDisabledByUserInput)
            {
                // The system disables a tap whose callback was too slow; turn it back on.
                var tap = _tap;
                if (tap != IntPtr.Zero && !_stopRequested)
                {
                    CGEventTapEnable(tap, true);
                }

                return cgEvent;
            }

            var callback = _callback;
            if (callback == null || type is not (KeyDownEvent or KeyUpEvent or FlagsChangedEvent))
            {
                return cgEvent;
            }

            var keyCode = (ushort)CGEventGetIntegerValueField(cgEvent, KeyboardEventKeycode);
            var flags = CGEventGetFlags(cgEvent);
            var state = ToSpecialKeyState(flags);

            bool continues;
            if (type == FlagsChangedEvent)
            {
                if (keyCode == KeyCodeCapsLock)
                {
                    // Caps Lock reports one flags change per press; Windows reports a down and an up.
                    continues = Notify(callback, KeyEvent.WM_KEYDOWN, keyCode, state);
                    continues &= Notify(callback, KeyEvent.WM_KEYUP, keyCode, state);
                }
                else if (TryGetDeviceModifierMask(keyCode, out var mask))
                {
                    var keyEvent = (flags & mask) != 0 ? KeyEvent.WM_KEYDOWN : KeyEvent.WM_KEYUP;
                    continues = Notify(callback, keyEvent, keyCode, state);
                }
                else
                {
                    return cgEvent;
                }
            }
            else
            {
                continues = Notify(callback, type == KeyDownEvent ? KeyEvent.WM_KEYDOWN : KeyEvent.WM_KEYUP, keyCode, state);
            }

            return continues || _listenOnly ? cgEvent : IntPtr.Zero;
        }
        catch (Exception e)
        {
            // Exceptions must not unwind into the event tap.
            Log.Exception(ClassName, "Keyboard event tap callback failed", e);
            return cgEvent;
        }
    }

    private static bool Notify(Func<KeyEvent, int, SpecialKeyState, bool> callback, KeyEvent keyEvent, ushort keyCode, SpecialKeyState state)
    {
        var virtualKey = ToVirtualKey(keyCode);
        return virtualKey == 0 || callback(keyEvent, virtualKey, state);
    }

    private static SpecialKeyState ToSpecialKeyState(ulong flags) => new()
    {
        CtrlPressed = (flags & ControlFlag) != 0,
        ShiftPressed = (flags & ShiftFlag) != 0,
        AltPressed = (flags & AlternateFlag) != 0,
        WinPressed = (flags & CommandFlag) != 0
    };

    private static bool TryGetDeviceModifierMask(ushort keyCode, out ulong mask)
    {
        mask = keyCode switch
        {
            KeyCodeControl => DeviceLeftControl,
            KeyCodeRightControl => DeviceRightControl,
            KeyCodeShift => DeviceLeftShift,
            KeyCodeRightShift => DeviceRightShift,
            KeyCodeCommand => DeviceLeftCommand,
            KeyCodeRightCommand => DeviceRightCommand,
            KeyCodeOption => DeviceLeftAlternate,
            KeyCodeRightOption => DeviceRightAlternate,
            _ => 0
        };
        return mask != 0;
    }

    /// <summary>
    /// Windows virtual key code for a macOS key code; 0 when the key has no Windows equivalent.
    /// </summary>
    private static int ToVirtualKey(ushort keyCode)
    {
        var table = _keyTable;
        return keyCode < table.Length ? ToVirtualKey(table[keyCode]) : 0;
    }

    private static int ToVirtualKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return 0x41 + (key - Key.A);
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return 0x30 + (key - Key.D0);
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return 0x60 + (key - Key.NumPad0);
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return 0x70 + (key - Key.F1);
        }

        return key switch
        {
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Clear => 0x0C,
            Key.Return => 0x0D,
            Key.KanaMode => 0x15,
            Key.HanjaMode => 0x19,
            Key.CapsLock => 0x14,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.End => 0x23,
            Key.Home => 0x24,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Help => 0x2F,
            Key.LWin => 0x5B,
            Key.RWin => 0x5C,
            Key.Apps => 0x5D,
            Key.Multiply => 0x6A,
            Key.Add => 0x6B,
            Key.Separator => 0x6C,
            Key.Subtract => 0x6D,
            Key.Decimal => 0x6E,
            Key.Divide => 0x6F,
            Key.LeftShift => 0xA0,
            Key.RightShift => 0xA1,
            Key.LeftCtrl => 0xA2,
            Key.RightCtrl => 0xA3,
            Key.LeftAlt => 0xA4,
            Key.RightAlt => 0xA5,
            Key.VolumeMute => 0xAD,
            Key.VolumeDown => 0xAE,
            Key.VolumeUp => 0xAF,
            Key.Oem1 => 0xBA,
            Key.OemPlus => 0xBB,
            Key.OemComma => 0xBC,
            Key.OemMinus => 0xBD,
            Key.OemPeriod => 0xBE,
            Key.Oem2 => 0xBF,
            Key.Oem3 => 0xC0,
            Key.AbntC1 => 0xC1,
            Key.Oem4 => 0xDB,
            Key.Oem5 => 0xDC,
            Key.Oem6 => 0xDD,
            Key.Oem7 => 0xDE,
            Key.Oem8 => 0xDF,
            Key.Oem102 => 0xE2,
            _ => 0
        };
    }

    /// <summary>
    /// Letters follow the keyboard layout, which can only be read on the main thread; until that is done keys map by
    /// their physical (ANSI) position.
    /// </summary>
    private static void LoadKeyTable()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _keyTable = MacKeyMap.CreateKeyCodeTable(followLayout: true);
            return;
        }

        _keyTable = MacKeyMap.CreateKeyCodeTable(followLayout: false);
        Dispatcher.UIThread.Post(() => _keyTable = MacKeyMap.CreateKeyCodeTable(followLayout: true));
    }

    private static void ObserveInputSourceChanges()
    {
        if (_observingInputSource)
        {
            return;
        }

        var name = InputSourceChangedNotification.Value;
        if (name == IntPtr.Zero)
        {
            return;
        }

        CFNotificationCenterAddObserver(CFNotificationCenterGetDistributedCenter(), ObserverToken,
            InputSourceChangedCallback, name, IntPtr.Zero, DeliverImmediately);
        _observingInputSource = true;
    }

    private static void StopObservingInputSourceChanges()
    {
        if (!_observingInputSource)
        {
            return;
        }

        CFNotificationCenterRemoveObserver(CFNotificationCenterGetDistributedCenter(), ObserverToken,
            InputSourceChangedNotification.Value, IntPtr.Zero);
        _observingInputSource = false;
    }

    private static void OnInputSourceChanged(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj, IntPtr userInfo)
    {
        Dispatcher.UIThread.Post(() => _keyTable = MacKeyMap.CreateKeyCodeTable(followLayout: true));
    }

    private static readonly Lazy<IntPtr> DefaultRunLoopMode = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(CoreFoundation), "kCFRunLoopDefaultMode")));

    private static readonly Lazy<IntPtr> InputSourceChangedNotification = new(() =>
        NativeLibrary.TryGetExport(NativeLibrary.Load(Carbon), "kTISNotifySelectedKeyboardInputSourceChanged", out var address)
            ? Marshal.ReadIntPtr(address)
            : IntPtr.Zero);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CGEventTapCallBack(IntPtr proxy, uint type, IntPtr cgEvent, IntPtr userInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CFNotificationCallback(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj, IntPtr userInfo);

    [DllImport(ApplicationServices)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventTapCreate(uint tap, uint place, uint options, ulong eventsOfInterest,
        CGEventTapCallBack callback, IntPtr userInfo);

    [DllImport(CoreGraphics)]
    private static extern void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enable);

    [DllImport(CoreGraphics)]
    private static extern long CGEventGetIntegerValueField(IntPtr cgEvent, uint field);

    [DllImport(CoreGraphics)]
    private static extern ulong CGEventGetFlags(IntPtr cgEvent);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, nint order);

    [DllImport(CoreFoundation)]
    private static extern void CFMachPortInvalidate(IntPtr port);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopRemoveSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    private static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSourceHandled);

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopStop(IntPtr runLoop);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFNotificationCenterGetDistributedCenter();

    [DllImport(CoreFoundation)]
    private static extern void CFNotificationCenterAddObserver(IntPtr center, IntPtr observer,
        CFNotificationCallback callback, IntPtr name, IntPtr obj, nint suspensionBehavior);

    [DllImport(CoreFoundation)]
    private static extern void CFNotificationCenterRemoveObserver(IntPtr center, IntPtr observer, IntPtr name, IntPtr obj);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFRetain(IntPtr value);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);
}
