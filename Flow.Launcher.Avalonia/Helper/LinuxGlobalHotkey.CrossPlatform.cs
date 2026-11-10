using System;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Picks the Linux global hotkey backend: the GlobalShortcuts portal in Wayland sessions (where X11 grabs only see
/// keys typed into XWayland windows), X11 key grabs otherwise. Without either, hotkeys cannot be registered and the
/// desktop's own shortcut settings can run <c>Flow.Launcher.Avalonia --toggle</c> instead (see SingleInstance).
/// </summary>
internal static class LinuxGlobalHotkey
{
    private static readonly string ClassName = nameof(LinuxGlobalHotkey);

    private static readonly TimeSpan PortalProbeTimeout = TimeSpan.FromSeconds(3);

    private static LinuxPortalShortcuts? _portal;
    private static LinuxX11Hotkeys? _x11;
    private static bool _initialized;

    private static bool IsWaylandSession =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
        || string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        // Runs once at startup: desktops match the window (WM_CLASS) to the entry for its name and icon, and the
        // portal only accepts the app id of an installed entry.
        var desktopEntryJustInstalled = LinuxDesktopEntry.EnsureInstalled();

        if (IsWaylandSession)
        {
            // Called on the UI thread during startup; the probe only awaits D-Bus I/O off this thread.
            var probe = Task.Run(() => LinuxPortalShortcuts.TryCreateAsync(desktopEntryJustInstalled));
            if (probe.Wait(PortalProbeTimeout))
            {
                _portal = probe.Result;
            }
            else
            {
                // A portal that answers this late is not used; release its bus connection when it does.
                probe.ContinueWith(static task => task.Result?.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            if (_portal is null)
            {
                Log.Warn(ClassName, "Wayland session without the GlobalShortcuts portal: global hotkeys are unavailable. "
                                    + $"Bind a desktop shortcut to \"Flow.Launcher.Avalonia {SingleInstance.ToggleArgument}\" instead.");
            }

            return;
        }

        _x11 = LinuxX11Hotkeys.TryCreate();
        if (_x11 is null)
        {
            Log.Warn(ClassName, "No X server connection: global hotkeys are unavailable");
        }
    }

    /// <summary>
    /// Registers a hotkey; returns its id or -1. With the portal, success means the shortcut was handed to the
    /// desktop, which binds it asynchronously (possibly after asking the user).
    /// </summary>
    internal static int Register(string hotkeyString, Action callback)
    {
        var hotkey = new HotkeyModel(hotkeyString);
        if (!hotkey.Validate(true) || !LinuxKeyMap.TryGetKeysym(hotkey.CharKey, out var keysym, out var keysymName))
        {
            Log.Warn(ClassName, $"Hotkey {hotkeyString} has no Linux key mapping");
            return -1;
        }

        if (_portal != null)
        {
            return _portal.Register(LinuxPortalShortcuts.ToShortcutId(hotkeyString), LinuxKeyMap.ToTrigger(hotkey, keysymName),
                $"Flow Launcher: {hotkeyString}", callback);
        }

        return _x11?.Register(keysym, LinuxKeyMap.ToX11Modifiers(hotkey), callback) ?? -1;
    }

    internal static bool Unregister(int hotkeyId) =>
        hotkeyId < 0 || (_portal?.Unregister(hotkeyId) ?? _x11?.Unregister(hotkeyId) ?? true);

    /// <summary>
    /// Whether registering the hotkey would succeed. The portal cannot be probed (the desktop decides when binding),
    /// so only the key mapping is checked there.
    /// </summary>
    internal static bool CheckAvailability(HotkeyModel hotkey)
    {
        if (!hotkey.Validate(true) || !LinuxKeyMap.TryGetKeysym(hotkey.CharKey, out var keysym, out _))
        {
            return false;
        }

        if (_portal != null)
        {
            return true;
        }

        if (_x11 is null)
        {
            return false;
        }

        var id = _x11.Register(keysym, LinuxKeyMap.ToX11Modifiers(hotkey), static () => { });
        if (id < 0)
        {
            return false;
        }

        _x11.Unregister(id);
        return true;
    }

    internal static bool IsForegroundWindowFullscreen() => _x11?.IsActiveWindowFullscreen() ?? false;

    /// <summary>
    /// Pointer position in physical pixels. Only X11 sessions report it; XWayland only knows the pointer while it is
    /// over an X11 window.
    /// </summary>
    internal static bool TryGetPointerPosition(out int x, out int y)
    {
        x = y = 0;
        return _x11?.TryGetPointerPosition(out x, out y) ?? false;
    }

    internal static void Shutdown()
    {
        _portal?.Dispose();
        _portal = null;
        _x11?.Dispose();
        _x11 = null;
        _initialized = false;
    }
}
