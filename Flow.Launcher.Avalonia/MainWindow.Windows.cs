using Avalonia;
using Avalonia.Platform;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Avalonia;

public partial class MainWindow
{
    private partial Screen? GetCursorScreen() => ScreenOf(MonitorInfo.GetCursorDisplayMonitor());

    private partial Screen? GetForegroundWindowScreen() =>
        ScreenOf(MonitorInfo.GetNearestDisplayMonitor(Win32Helper.GetForegroundWindow()));

    // Avalonia screen coordinates on Windows are the same physical virtual-screen pixels Win32 reports.
    private Screen? ScreenOf(MonitorInfo? monitor)
    {
        if (monitor == null)
        {
            return null;
        }

        var bounds = monitor.Bounds;
        return Screens.ScreenFromPoint(new PixelPoint(
            (int)(bounds.X + bounds.Width / 2),
            (int)(bounds.Y + bounds.Height / 2)));
    }
}
