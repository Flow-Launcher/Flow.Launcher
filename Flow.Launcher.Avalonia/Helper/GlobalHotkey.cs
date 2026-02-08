using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Win32-based global hotkey manager for Avalonia.
/// Uses RegisterHotKey/UnregisterHotKey Win32 APIs with a message-only window.
/// </summary>
public static class GlobalHotkey
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    // Modifier keys for RegisterHotKey
    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000
    }

    // Virtual key codes
    public static class VirtualKeys
    {
        public const uint Space = 0x20;
        public const uint Enter = 0x0D;
        public const uint Tab = 0x09;
        public const uint A = 0x41;
        public const uint B = 0x42;
        public const uint C = 0x43;
        public const uint D = 0x44;
        public const uint E = 0x45;
        public const uint F = 0x46;
        public const uint G = 0x47;
        public const uint H = 0x48;
        public const uint I = 0x49;
        public const uint J = 0x4A;
        public const uint K = 0x4B;
        public const uint L = 0x4C;
        public const uint M = 0x4D;
        public const uint N = 0x4E;
        public const uint O = 0x4F;
        public const uint P = 0x50;
        public const uint Q = 0x51;
        public const uint R = 0x52;
        public const uint S = 0x53;
        public const uint T = 0x54;
        public const uint U = 0x55;
        public const uint V = 0x56;
        public const uint W = 0x57;
        public const uint X = 0x58;
        public const uint Y = 0x59;
        public const uint Z = 0x5A;
    }

    private static IntPtr _messageWindow;
    private static WndProcDelegate? _wndProc; // prevent GC
    private static readonly Dictionary<int, Action> _hotkeyCallbacks = new();
    private static int _nextHotkeyId = 1;
    private static DispatcherTimer? _messageTimer;
    private static bool _initialized;

    /// <summary>
    /// Initialize the hotkey system. Call once at app startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        _wndProc = WndProc;

        var wc = new WNDCLASS
        {
            lpfnWndProc = _wndProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = "FlowLauncherAvaloniaHotkeyClass"
        };

        if (RegisterClass(ref wc) == 0)
        {
            Console.WriteLine("[GlobalHotkey] Failed to register window class");
            return;
        }

        // Create message-only window (HWND_MESSAGE = -3)
        _messageWindow = CreateWindowEx(
            0, wc.lpszClassName, "FlowLauncherHotkeyWindow",
            0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        if (_messageWindow == IntPtr.Zero)
        {
            Console.WriteLine("[GlobalHotkey] Failed to create message window");
            return;
        }

        // Poll for messages periodically (hotkeys come via WM_HOTKEY)
        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _messageTimer.Tick += (_, _) => ProcessMessages();
        _messageTimer.Start();

        _initialized = true;
        Console.WriteLine("[GlobalHotkey] Initialized successfully");
    }

    /// <summary>
    /// Register a global hotkey.
    /// </summary>
    /// <param name="modifiers">Modifier keys (Alt, Ctrl, Shift, Win)</param>
    /// <param name="key">Virtual key code</param>
    /// <param name="callback">Action to invoke when hotkey is pressed</param>
    /// <returns>Hotkey ID for later unregistration, or -1 on failure</returns>
    public static int Register(Modifiers modifiers, uint key, Action callback)
    {
        if (!_initialized)
        {
            Console.WriteLine("[GlobalHotkey] Not initialized");
            return -1;
        }

        int id = _nextHotkeyId++;
        
        // Add NoRepeat to prevent repeated triggers while held
        uint mods = (uint)modifiers | (uint)Modifiers.NoRepeat;

        if (!RegisterHotKey(_messageWindow, id, mods, key))
        {
            int error = Marshal.GetLastWin32Error();
            Console.WriteLine($"[GlobalHotkey] Failed to register hotkey (error {error})");
            return -1;
        }

        _hotkeyCallbacks[id] = callback;
        Console.WriteLine($"[GlobalHotkey] Registered hotkey id={id}, mods={modifiers}, key=0x{key:X2}");
        return id;
    }

    /// <summary>
    /// Unregister a previously registered hotkey.
    /// </summary>
    public static void Unregister(int hotkeyId)
    {
        if (hotkeyId < 0 || _messageWindow == IntPtr.Zero) return;

        UnregisterHotKey(_messageWindow, hotkeyId);
        _hotkeyCallbacks.Remove(hotkeyId);
        Console.WriteLine($"[GlobalHotkey] Unregistered hotkey id={hotkeyId}");
    }

    /// <summary>
    /// Cleanup all hotkeys and resources.
    /// </summary>
    public static void Shutdown()
    {
        _messageTimer?.Stop();

        foreach (var id in _hotkeyCallbacks.Keys)
        {
            UnregisterHotKey(_messageWindow, id);
        }
        _hotkeyCallbacks.Clear();

        if (_messageWindow != IntPtr.Zero)
        {
            DestroyWindow(_messageWindow);
            _messageWindow = IntPtr.Zero;
        }

        _initialized = false;
        Console.WriteLine("[GlobalHotkey] Shutdown complete");
    }

    private static void ProcessMessages()
    {
        while (PeekMessage(out var msg, _messageWindow, 0, 0, 1)) // PM_REMOVE = 1
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyCallbacks.TryGetValue(id, out var callback))
            {
                Console.WriteLine($"[GlobalHotkey] Hotkey triggered id={id}");
                Dispatcher.UIThread.Post(() => callback());
            }
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Parse a hotkey string like "Alt + Space" into modifiers and key.
    /// </summary>
    public static (Modifiers mods, uint key) ParseHotkeyString(string hotkeyString)
    {
        var mods = Modifiers.None;
        uint key = 0;

        var parts = hotkeyString.Replace(" ", "").Split('+');
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "alt": mods |= Modifiers.Alt; break;
                case "ctrl": 
                case "control": mods |= Modifiers.Control; break;
                case "shift": mods |= Modifiers.Shift; break;
                case "win":
                case "windows": mods |= Modifiers.Win; break;
                case "space": key = VirtualKeys.Space; break;
                case "enter":
                case "return": key = VirtualKeys.Enter; break;
                case "tab": key = VirtualKeys.Tab; break;
                default:
                    // Try single letter
                    if (part.Length == 1 && char.IsLetter(part[0]))
                    {
                        key = (uint)char.ToUpperInvariant(part[0]);
                    }
                    break;
            }
        }

        return (mods, key);
    }
}
