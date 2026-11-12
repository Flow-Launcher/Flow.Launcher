using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Global hotkeys on X11 through key grabs on the root window. Uses its own XCB connection: XCB reports a refused
/// grab (another client owns the combination) per request, whereas Xlib only reports it through the process-wide
/// error handler that Avalonia's X11 backend owns. XCB connections are thread-safe, so grabs are made on the
/// calling thread while a background thread reads key events.
/// </summary>
internal sealed class LinuxX11Hotkeys : IDisposable
{
    private static readonly string ClassName = nameof(LinuxX11Hotkeys);

    private const byte KeyPress = 2;
    private const byte KeyRelease = 3;
    private const byte MappingNotify = 34;
    private const byte GrabModeAsync = 1;
    private const byte BadAccess = 10;

    // Num Lock and Caps Lock change the reported modifier state, so every grab is repeated with each combination.
    private static readonly ushort[] IgnoredModifierCombinations =
        [0, LinuxKeyMap.LockMask, LinuxKeyMap.Mod2Mask, LinuxKeyMap.LockMask | LinuxKeyMap.Mod2Mask];

    private sealed record Registration(uint Keysym, ushort Modifiers, Action Callback)
    {
        public List<byte> Keycodes { get; } = new();
    }

    private readonly IntPtr _connection;
    private readonly uint _root;
    private readonly byte _minKeycode;
    private readonly byte _maxKeycode;
    private readonly Lock _lock = new();
    private readonly Dictionary<int, Registration> _registrations = new();
    private readonly int _wakeFd;
    private readonly Thread _eventThread;
    private uint[] _keysymsByKeycode = [];
    private int _keysymsPerKeycode;
    private int _nextId;
    private uint _lastReleaseTime;
    private byte _lastReleaseKeycode;
    private volatile bool _disposed;

    private LinuxX11Hotkeys(IntPtr connection, uint root, byte minKeycode, byte maxKeycode, int wakeFd)
    {
        _connection = connection;
        _root = root;
        _minKeycode = minKeycode;
        _maxKeycode = maxKeycode;
        _wakeFd = wakeFd;
        LoadKeyboardMapping();

        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "X11 hotkeys" };
        _eventThread.Start();
    }

    /// <summary>
    /// Connects to the X server named by $DISPLAY; null when there is none or libxcb is unavailable.
    /// </summary>
    internal static LinuxX11Hotkeys? TryCreate()
    {
        try
        {
            var connection = xcb_connect(null, out var screenNumber);
            if (xcb_connection_has_error(connection) != 0)
            {
                xcb_disconnect(connection);
                Log.Warn(ClassName, "Cannot connect to the X server");
                return null;
            }

            var setup = xcb_get_setup(connection);
            var iterator = xcb_setup_roots_iterator(setup);
            for (var i = 0; i < screenNumber && iterator.rem > 0; i++)
            {
                xcb_screen_next(ref iterator);
            }

            // xcb_screen_t starts with the root window id; xcb_setup_t holds min/max keycode at bytes 34/35.
            var root = (uint)Marshal.ReadInt32(iterator.data);
            var minKeycode = Marshal.ReadByte(setup, 34);
            var maxKeycode = Marshal.ReadByte(setup, 35);

            var wakeFd = eventfd(0, EfdCloexec | EfdNonblock);
            if (wakeFd < 0)
            {
                xcb_disconnect(connection);
                Log.Error(ClassName, $"eventfd failed (errno {Marshal.GetLastPInvokeError()})");
                return null;
            }

            return new LinuxX11Hotkeys(connection, root, minKeycode, maxKeycode, wakeFd);
        }
        catch (DllNotFoundException e)
        {
            Log.Warn(ClassName, $"libxcb is not available: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Grabs the key combination. Returns the hotkey id, or -1 when the key has no keycode in the current keymap
    /// or another client (or this one) already grabbed the combination.
    /// </summary>
    internal int Register(uint keysym, ushort modifiers, Action callback)
    {
        lock (_lock)
        {
            // Re-grabbing a combination this client already holds succeeds silently, and the later ungrab of either
            // registration would release both; treat it as taken, like an exclusive Carbon hotkey on macOS.
            foreach (var existing in _registrations.Values)
            {
                if (existing.Keysym == keysym && existing.Modifiers == modifiers)
                {
                    return -1;
                }
            }

            var registration = new Registration(keysym, modifiers, callback);
            if (!Grab(registration))
            {
                return -1;
            }

            var id = _nextId++;
            _registrations[id] = registration;
            return id;
        }
    }

    internal bool Unregister(int id)
    {
        lock (_lock)
        {
            if (!_registrations.Remove(id, out var registration))
            {
                return true;
            }

            Ungrab(registration);
            return true;
        }
    }

    private bool Grab(Registration registration)
    {
        foreach (var keycode in KeycodesFor(registration.Keysym))
        {
            foreach (var ignored in IgnoredModifierCombinations)
            {
                var cookie = xcb_grab_key_checked(_connection, 1, _root, (ushort)(registration.Modifiers | ignored), keycode,
                    GrabModeAsync, GrabModeAsync);
                var error = xcb_request_check(_connection, cookie);
                if (error == IntPtr.Zero)
                {
                    continue;
                }

                var code = Marshal.ReadByte(error, 1);
                free(error);
                Log.Warn(ClassName, $"Grabbing keycode {keycode} (keysym 0x{registration.Keysym:X}, modifiers 0x{registration.Modifiers:X}) failed with X error {code}"
                                    + (code == BadAccess ? " (already grabbed by another application)" : string.Empty));
                // Release this keycode's already grabbed variants too; UngrabKey only touches this client's grabs.
                registration.Keycodes.Add(keycode);
                Ungrab(registration);
                return false;
            }

            registration.Keycodes.Add(keycode);
        }

        if (registration.Keycodes.Count == 0)
        {
            Log.Warn(ClassName, $"Keysym 0x{registration.Keysym:X} is not on the current keyboard layout");
            return false;
        }

        return true;
    }

    private void Ungrab(Registration registration)
    {
        foreach (var keycode in registration.Keycodes)
        {
            foreach (var ignored in IgnoredModifierCombinations)
            {
                xcb_ungrab_key(_connection, keycode, _root, (ushort)(registration.Modifiers | ignored));
            }
        }

        registration.Keycodes.Clear();
        xcb_flush(_connection);
    }

    // Keycodes whose unshifted symbol (first column) is the keysym; letters are reported lowercase there.
    private IEnumerable<byte> KeycodesFor(uint keysym)
    {
        var keycodes = new List<byte>();
        for (var index = 0; index * _keysymsPerKeycode < _keysymsByKeycode.Length; index++)
        {
            if (_keysymsByKeycode[index * _keysymsPerKeycode] == keysym)
            {
                keycodes.Add((byte)(_minKeycode + index));
            }
        }

        return keycodes;
    }

    private void LoadKeyboardMapping()
    {
        var count = (byte)(_maxKeycode - _minKeycode + 1);
        var cookie = xcb_get_keyboard_mapping(_connection, _minKeycode, count);
        var reply = xcb_get_keyboard_mapping_reply(_connection, cookie, IntPtr.Zero);
        if (reply == IntPtr.Zero)
        {
            Log.Error(ClassName, "GetKeyboardMapping failed");
            return;
        }

        try
        {
            var length = xcb_get_keyboard_mapping_keysyms_length(reply);
            var keysyms = new int[length];
            Marshal.Copy(xcb_get_keyboard_mapping_keysyms(reply), keysyms, 0, length);
            _keysymsByKeycode = Array.ConvertAll(keysyms, keysym => (uint)keysym);
            _keysymsPerKeycode = Math.Max(1, (int)Marshal.ReadByte(reply, 1));
        }
        finally
        {
            free(reply);
        }
    }

    private void OnMappingChanged()
    {
        lock (_lock)
        {
            foreach (var registration in _registrations.Values)
            {
                Ungrab(registration);
            }

            LoadKeyboardMapping();

            foreach (var registration in _registrations.Values)
            {
                Grab(registration);
            }
        }
    }

    private void EventLoop()
    {
        var fds = new PollFd[]
        {
            new() { fd = xcb_get_file_descriptor(_connection), events = PollIn },
            new() { fd = _wakeFd, events = PollIn },
        };

        while (!_disposed)
        {
            IntPtr ev;
            while (!_disposed && (ev = xcb_poll_for_event(_connection)) != IntPtr.Zero)
            {
                try
                {
                    HandleEvent(ev);
                }
                catch (Exception e)
                {
                    Log.Exception(ClassName, "Hotkey event handler failed", e);
                }
                finally
                {
                    free(ev);
                }
            }

            if (_disposed)
            {
                return;
            }

            if (xcb_connection_has_error(_connection) != 0)
            {
                Log.Error(ClassName, "X server connection lost; global hotkeys stop working");
                return;
            }

            fds[0].revents = 0;
            fds[1].revents = 0;
            if (poll(fds, (nuint)fds.Length, -1) < 0 && Marshal.GetLastPInvokeError() != EIntr)
            {
                Log.Error(ClassName, $"poll failed (errno {Marshal.GetLastPInvokeError()})");
                return;
            }
        }
    }

    private void HandleEvent(IntPtr ev)
    {
        var type = (byte)(Marshal.ReadByte(ev, 0) & 0x7f);
        switch (type)
        {
            case MappingNotify:
                OnMappingChanged();
                return;
            case KeyRelease:
                _lastReleaseKeycode = Marshal.ReadByte(ev, 1);
                _lastReleaseTime = (uint)Marshal.ReadInt32(ev, 4);
                return;
            case KeyPress:
                break;
            default:
                return;
        }

        // xcb_key_press_event_t: detail (keycode) at 1, time at 4, state at 28.
        var keycode = Marshal.ReadByte(ev, 1);
        var time = (uint)Marshal.ReadInt32(ev, 4);
        var state = (ushort)(Marshal.ReadInt16(ev, 28) & LinuxKeyMap.RelevantModifiers);

        // Auto-repeat delivers a release and a press with the same timestamp; only the first press counts.
        if (keycode == _lastReleaseKeycode && time == _lastReleaseTime)
        {
            return;
        }

        Action? callback = null;
        lock (_lock)
        {
            foreach (var registration in _registrations.Values)
            {
                if (registration.Modifiers == state && registration.Keycodes.Contains(keycode))
                {
                    callback = registration.Callback;
                    break;
                }
            }
        }

        if (callback != null)
        {
            Dispatcher.UIThread.Post(callback);
        }
    }

    /// <summary>
    /// Whether the active window (EWMH _NET_ACTIVE_WINDOW) is in fullscreen state.
    /// </summary>
    internal bool IsActiveWindowFullscreen()
    {
        var activeWindowAtom = InternAtom("_NET_ACTIVE_WINDOW");
        var stateAtom = InternAtom("_NET_WM_STATE");
        var fullscreenAtom = InternAtom("_NET_WM_STATE_FULLSCREEN");
        if (activeWindowAtom == 0 || stateAtom == 0 || fullscreenAtom == 0)
        {
            return false;
        }

        var activeWindow = GetCardinalProperty(_root, activeWindowAtom);
        if (activeWindow is not [var window, ..] || window == 0)
        {
            return false;
        }

        return GetCardinalProperty(window, stateAtom) is { } states && Array.IndexOf(states, fullscreenAtom) >= 0;
    }

    /// <summary>
    /// Pointer position on the root window, in physical pixels.
    /// </summary>
    internal bool TryGetPointerPosition(out int x, out int y)
    {
        x = y = 0;
        var reply = xcb_query_pointer_reply(_connection, xcb_query_pointer(_connection, _root), IntPtr.Zero);
        if (reply == IntPtr.Zero)
        {
            return false;
        }

        // xcb_query_pointer_reply_t: same_screen at 1, root_x/root_y at 16/18.
        var sameScreen = Marshal.ReadByte(reply, 1) != 0;
        x = Marshal.ReadInt16(reply, 16);
        y = Marshal.ReadInt16(reply, 18);
        free(reply);
        return sameScreen;
    }

    private uint InternAtom(string name)
    {
        var reply = xcb_intern_atom_reply(_connection, xcb_intern_atom(_connection, 1, (ushort)name.Length, name), IntPtr.Zero);
        if (reply == IntPtr.Zero)
        {
            return 0;
        }

        // xcb_intern_atom_reply_t: atom at 8.
        var atom = (uint)Marshal.ReadInt32(reply, 8);
        free(reply);
        return atom;
    }

    // Reads a 32-bit format property (WINDOW / ATOM / CARDINAL lists).
    private uint[]? GetCardinalProperty(uint window, uint property)
    {
        const uint anyPropertyType = 0;
        var reply = xcb_get_property_reply(_connection,
            xcb_get_property(_connection, 0, window, property, anyPropertyType, 0, 1024), IntPtr.Zero);
        if (reply == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            // xcb_get_property_reply_t: format at 1.
            if (Marshal.ReadByte(reply, 1) != 32)
            {
                return null;
            }

            var count = xcb_get_property_value_length(reply) / 4;
            var values = new int[count];
            Marshal.Copy(xcb_get_property_value(reply), values, 0, count);
            return Array.ConvertAll(values, value => (uint)value);
        }
        finally
        {
            free(reply);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            foreach (var registration in _registrations.Values)
            {
                Ungrab(registration);
            }

            _registrations.Clear();
        }

        _disposed = true;
        ulong one = 1;
        write(_wakeFd, ref one, sizeof(ulong));
        _eventThread.Join(TimeSpan.FromSeconds(1));
        close(_wakeFd);
        xcb_disconnect(_connection);
    }

    private const string Xcb = "libxcb.so.1";
    private const string LibC = "libc";
    private const short PollIn = 0x001;
    private const int EIntr = 4;
    private const int EfdCloexec = 0x80000;
    private const int EfdNonblock = 0x800;

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreenIterator
    {
        public IntPtr data;
        public int rem;
        public int index;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Cookie
    {
        public uint sequence;
    }

    [DllImport(Xcb)]
    private static extern IntPtr xcb_connect(string? displayName, out int screen);

    [DllImport(Xcb)]
    private static extern int xcb_connection_has_error(IntPtr connection);

    [DllImport(Xcb)]
    private static extern void xcb_disconnect(IntPtr connection);

    [DllImport(Xcb)]
    private static extern int xcb_flush(IntPtr connection);

    [DllImport(Xcb)]
    private static extern int xcb_get_file_descriptor(IntPtr connection);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_get_setup(IntPtr connection);

    [DllImport(Xcb)]
    private static extern ScreenIterator xcb_setup_roots_iterator(IntPtr setup);

    [DllImport(Xcb)]
    private static extern void xcb_screen_next(ref ScreenIterator iterator);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_poll_for_event(IntPtr connection);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_request_check(IntPtr connection, Cookie cookie);

    [DllImport(Xcb)]
    private static extern Cookie xcb_grab_key_checked(IntPtr connection, byte ownerEvents, uint grabWindow, ushort modifiers,
        byte key, byte pointerMode, byte keyboardMode);

    [DllImport(Xcb)]
    private static extern Cookie xcb_ungrab_key(IntPtr connection, byte key, uint grabWindow, ushort modifiers);

    [DllImport(Xcb)]
    private static extern Cookie xcb_get_keyboard_mapping(IntPtr connection, byte firstKeycode, byte count);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_get_keyboard_mapping_reply(IntPtr connection, Cookie cookie, IntPtr error);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_get_keyboard_mapping_keysyms(IntPtr reply);

    [DllImport(Xcb)]
    private static extern int xcb_get_keyboard_mapping_keysyms_length(IntPtr reply);

    [DllImport(Xcb)]
    private static extern Cookie xcb_query_pointer(IntPtr connection, uint window);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_query_pointer_reply(IntPtr connection, Cookie cookie, IntPtr error);

    [DllImport(Xcb)]
    private static extern Cookie xcb_intern_atom(IntPtr connection, byte onlyIfExists, ushort nameLength,
        [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_intern_atom_reply(IntPtr connection, Cookie cookie, IntPtr error);

    [DllImport(Xcb)]
    private static extern Cookie xcb_get_property(IntPtr connection, byte delete, uint window, uint property, uint type,
        uint longOffset, uint longLength);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_get_property_reply(IntPtr connection, Cookie cookie, IntPtr error);

    [DllImport(Xcb)]
    private static extern IntPtr xcb_get_property_value(IntPtr reply);

    [DllImport(Xcb)]
    private static extern int xcb_get_property_value_length(IntPtr reply);

    [DllImport(LibC)]
    private static extern void free(IntPtr pointer);

    [DllImport(LibC, SetLastError = true)]
    private static extern int eventfd(uint initialValue, int flags);

    [DllImport(LibC, SetLastError = true)]
    private static extern int poll([In, Out] PollFd[] fds, nuint count, int timeout);

    [DllImport(LibC)]
    private static extern nint write(int fd, ref ulong value, nuint count);

    [DllImport(LibC)]
    private static extern int close(int fd);
}
