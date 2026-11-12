using System;
using Avalonia;

namespace Flow.Launcher.Avalonia;

internal sealed partial class Program
{
    /// <summary>
    /// Set to "x11" to run through XWayland in a Wayland session (Avalonia's Wayland backend is still young).
    /// </summary>
    private const string BackendVariable = "FLOW_LAUNCHER_BACKEND";

    /// <summary>
    /// Whether windows use Avalonia's Wayland backend: a Wayland session that did not opt out through
    /// <see cref="BackendVariable"/>. (UseWaylandWithFallback only falls back when the compositor is unreachable.)
    /// </summary>
    internal static bool UsesNativeWayland { get; private set; }

    // Native Wayland in Wayland sessions; UseWaylandWithFallback keeps X11 when no compositor is reachable.
    static partial void UseNativeWayland(ref AppBuilder builder)
    {
        if (!OperatingSystem.IsLinux()
            || string.Equals(Environment.GetEnvironmentVariable(BackendVariable), "x11", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        builder = builder
            .With(new WaylandPlatformOptions { AppId = LinuxAppId })
            .UseWaylandWithFallback();
        UsesNativeWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }
}
