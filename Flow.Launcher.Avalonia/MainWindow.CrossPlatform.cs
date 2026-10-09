using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Flow.Launcher.Avalonia.Helper;

namespace Flow.Launcher.Avalonia;

public partial class MainWindow
{
    // Window.Activate() only makes the window key inside this app; macOS also needs the app itself activated
    // when the launcher is summoned by a global hotkey while another app is frontmost.
    partial void ActivateApplication()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacWindowActivation.ActivateForWindow(this);
        }
    }

    // A hidden window leaves the app active with no window to focus; hide the app so the previous app gets focus
    // back, unless another Flow Launcher window (e.g. settings) is still open.
    partial void ReturnFocusAfterHide()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.Windows.Any(window => window != this && window.IsVisible))
        {
            return;
        }

        MacWindowActivation.ReturnFocusToPreviousApp();
    }

    private partial Screen? GetCursorScreen() =>
        OperatingSystem.IsMacOS() && MacScreens.TryGetCursorLocation(out var x, out var y) ? ScreenAtMacPoint(x, y) : null;

    private partial Screen? GetForegroundWindowScreen() =>
        OperatingSystem.IsMacOS() && MacScreens.TryGetFrontmostWindowCenter(out var x, out var y) ? ScreenAtMacPoint(x, y) : null;

    // CoreGraphics reports global points; Avalonia reports each screen's bounds in that screen's pixels (points × scaling).
    private Screen? ScreenAtMacPoint(double x, double y) =>
        Screens.All.FirstOrDefault(screen =>
        {
            var bounds = screen.Bounds.ToRect(screen.Scaling);
            return bounds.Contains(new Point(x, y));
        });
}
