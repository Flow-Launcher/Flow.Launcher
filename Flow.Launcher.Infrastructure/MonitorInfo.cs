using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure;

/// <summary>
/// Contains full information about a display monitor.
/// Codes are edited from: <see href="https://github.com/Jack251970/DesktopWidgets3">.
/// </summary>
internal class MonitorInfo
{
    /// <summary>
    /// Gets the display monitors (including invisible pseudo-monitors associated with the mirroring drivers).
    /// </summary>
    /// <returns>A list of display monitors</returns>
    public static unsafe IList<MonitorInfo> GetDisplayMonitors()
    {
        var monitorCount = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CMONITORS);
        var list = new List<MonitorInfo>(monitorCount);
        var callback = new MONITORENUMPROC((HMONITOR monitor, HDC deviceContext, RECT* rect, LPARAM data) =>
        {
            list.Add(new MonitorInfo(monitor, rect));
            return true;
        });
        var dwData = new LPARAM();
        var hdc = new HDC();
        bool ok = PInvoke.EnumDisplayMonitors(hdc, (RECT?)null, callback, dwData);
        if (!ok)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
        return list;
    }

    /// <summary>
    /// Gets the display monitor that is nearest to a given window.
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <returns>The display monitor that is nearest to a given window, or null if no monitor is found.</returns>
    public static unsafe MonitorInfo GetNearestDisplayMonitor(HWND hwnd)
    {
        var nearestMonitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MonitorInfo nearestMonitorInfo = null;
        var callback = new MONITORENUMPROC((HMONITOR monitor, HDC deviceContext, RECT* rect, LPARAM data) =>
        {
            if (monitor == nearestMonitor)
            {
                nearestMonitorInfo = new MonitorInfo(monitor, rect);
                return false;
            }
            return true;
        });
        var dwData = new LPARAM();
        var hdc = new HDC();
        bool ok = PInvoke.EnumDisplayMonitors(hdc, (RECT?)null, callback, dwData);
        if (!ok)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
        return nearestMonitorInfo;
    }

    private readonly HMONITOR _monitor;

    internal unsafe MonitorInfo(HMONITOR monitor, RECT* rect)
    {
        RectMonitor =
            new Rect(new Point(rect->left, rect->top),
            new Point(rect->right, rect->bottom));
        _monitor = monitor;
        var info = new MONITORINFOEXW() { monitorInfo = new MONITORINFO() { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        GetMonitorInfo(monitor, ref info);
        RectWork =
            new Rect(new Point(info.monitorInfo.rcWork.left, info.monitorInfo.rcWork.top),
            new Point(info.monitorInfo.rcWork.right, info.monitorInfo.rcWork.bottom));
        Name = new string(info.szDevice.AsSpan()).Replace("\0", "").Trim();
    }

    /// <summary>
    /// Gets the name of the display.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the display monitor rectangle, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// <note>If the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.</note>
    /// </remarks>
    public Rect RectMonitor { get; }

    /// <summary>
    /// Gets the work area rectangle of the display monitor, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// <note>If the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.</note>
    /// </remarks>
    public Rect RectWork { get; }

    /// <summary>
    /// Gets if the monitor is the the primary display monitor.
    /// </summary>
    public bool IsPrimary => _monitor == PInvoke.MonitorFromWindow(new(IntPtr.Zero), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

    /// <inheritdoc />
    public override string ToString() => $"{Name} {RectMonitor.Width}x{RectMonitor.Height}";

    private static unsafe bool GetMonitorInfo(HMONITOR hMonitor, ref MONITORINFOEXW lpmi)
    {
        fixed (MONITORINFOEXW* lpmiLocal = &lpmi)
        {
            var lpmiBase = (MONITORINFO*)lpmiLocal;
            var __result = PInvoke.GetMonitorInfo(hMonitor, lpmiBase);
            return __result;
        }
    }
}
