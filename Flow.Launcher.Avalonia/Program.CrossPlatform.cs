using System;
using Avalonia;

namespace Flow.Launcher.Avalonia;

internal sealed partial class Program
{
    /// <summary>
    /// Set to "x11" to run through XWayland in a Wayland session (Avalonia's Wayland backend is still young).
    /// </summary>
    private const string BackendVariable = "FLOW_LAUNCHER_BACKEND";

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
    }
}
