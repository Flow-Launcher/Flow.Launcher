using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Cmd+Alt+Shift+R (Ctrl+Alt+Shift+R elsewhere) toggles Avalonia's FPS / layout-time / render-time
/// overlays on every open window (and windows opened while on), and records a <see cref="RenderTrace"/>
/// for as long as the overlay is on.
/// </summary>
internal static class RenderDiagnosticsHotkey
{
    private const RendererDebugOverlays Overlays =
        RendererDebugOverlays.Fps | RendererDebugOverlays.LayoutTimeGraph | RendererDebugOverlays.RenderTimeGraph;

    private static bool _enabled;

    public static void Register()
    {
        InputElement.KeyDownEvent.AddClassHandler<Window>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => Apply(window));
    }

    private static void OnKeyDown(Window window, KeyEventArgs e)
    {
        var primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key != Key.R || e.KeyModifiers != (primary | KeyModifiers.Alt | KeyModifiers.Shift))
            return;

        e.Handled = true;
        _enabled = !_enabled;
        if (_enabled)
            RenderTrace.Start();
        else
            RenderTrace.Stop();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var w in desktop.Windows)
                Apply(w);
        }
        Apply(window);
    }

    private static void Apply(Window window) =>
        window.RendererDiagnostics.DebugOverlays = _enabled ? Overlays : RendererDebugOverlays.None;
}
