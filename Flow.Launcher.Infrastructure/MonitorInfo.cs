using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure;

/// <summary>
/// Contains full information about a display monitor.
/// Inspired from: https://github.com/Jack251970/DesktopWidgets3.
/// </summary>
/// <remarks>
/// Use this class to replace the System.Windows.Forms.Screen class which can cause possible System.PlatformNotSupportedException.
/// </remarks>
public class MonitorInfo
{
    /// <summary>
    /// Gets the display monitors (including invisible pseudo-monitors associated with the mirroring drivers).
    /// </summary>
    /// <returns>A list of display monitors</returns>
    public static unsafe IList<MonitorInfo> GetDisplayMonitors()
    {
        var monitorCount = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CMONITORS);
        var list = new List<MonitorInfo>(monitorCount);
        var callback = new MONITORENUMPROC((monitor, deviceContext, rect, data) =>
        {
            list.Add(new MonitorInfo(monitor, rect));
            return true;
        });
        var dwData = new LPARAM();
        var hdc = new HDC();
        bool ok = PInvoke.EnumDisplayMonitors(hdc, null, callback, dwData);
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
    public static unsafe MonitorInfo GetNearestDisplayMonitor(nint hwnd)
    {
        var nearestMonitor = PInvoke.MonitorFromWindow(new(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MonitorInfo nearestMonitorInfo = null;
        var callback = new MONITORENUMPROC((monitor, deviceContext, rect, data) =>
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
        bool ok = PInvoke.EnumDisplayMonitors(hdc, null, callback, dwData);
        if (!ok)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
        return nearestMonitorInfo;
    }

    /// <summary>
    /// Gets the primary display monitor (the one that contains the taskbar).
    /// </summary>
    /// <returns>The primary display monitor, or null if no monitor is found.</returns>
    public static unsafe MonitorInfo GetPrimaryDisplayMonitor()
    {
        var primaryMonitor = PInvoke.MonitorFromWindow(new HWND(IntPtr.Zero), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        MonitorInfo primaryMonitorInfo = null;
        var callback = new MONITORENUMPROC((monitor, deviceContext, rect, data) =>
        {
            if (monitor == primaryMonitor)
            {
                primaryMonitorInfo = new MonitorInfo(monitor, rect);
                return false;
            }
            return true;
        });
        var dwData = new LPARAM();
        var hdc = new HDC();
        bool ok = PInvoke.EnumDisplayMonitors(hdc, null, callback, dwData);
        if (!ok)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
        return primaryMonitorInfo;
    }

    /// <summary>
    /// Gets the display monitor that contains the cursor.
    /// </summary>
    /// <returns>The display monitor that contains the cursor, or null if no monitor is found.</returns>
    public static unsafe MonitorInfo GetCursorDisplayMonitor()
    {
        if (!PInvoke.GetCursorPos(out var pt))
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
        var cursorMonitor = PInvoke.MonitorFromPoint(pt, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MonitorInfo cursorMonitorInfo = null;
        var callback = new MONITORENUMPROC((monitor, deviceContext, rect, data) =>
        {
            if (monitor == cursorMonitor)
            {
                cursorMonitorInfo = new MonitorInfo(monitor, rect);
                return false;
            }
            return true;
        });
        var dwData = new LPARAM();
        var hdc = new HDC();
        bool ok = PInvoke.EnumDisplayMonitors(hdc, null, callback, dwData);
        if (!ok)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
        return cursorMonitorInfo;
    }

    private readonly HMONITOR _monitor;

    internal unsafe MonitorInfo(HMONITOR monitor, RECT* rect)
    {
        Bounds =
            new Rect(new Point(rect->left, rect->top),
            new Point(rect->right, rect->bottom));
        _monitor = monitor;
        var info = new MONITORINFOEXW() { monitorInfo = new MONITORINFO() { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        GetMonitorInfo(monitor, ref info);
        WorkingArea =
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
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the work area rectangle of the display monitor, expressed in virtual-screen coordinates.
    /// </summary>
    /// <remarks>
    /// <note>If the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.</note>
    /// </remarks>
    public Rect WorkingArea { get; }

    /// <summary>
    /// Gets if the monitor is the primary display monitor.
    /// </summary>
    public bool IsPrimary => _monitor == PInvoke.MonitorFromWindow(new(IntPtr.Zero), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

    /// <inheritdoc />
    public override string ToString() => $"{Name} {Bounds.Width}x{Bounds.Height}";

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
