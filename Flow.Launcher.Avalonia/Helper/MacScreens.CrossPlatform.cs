using System;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// CoreGraphics queries for choosing the display the launcher opens on. Locations are in global display points with
/// the origin at the top-left corner of the main display, y pointing down. None of these calls need extra permissions.
/// </summary>
internal static class MacScreens
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // CGWindowListOption
    private const uint OnScreenOnly = 1 << 0;
    private const uint ExcludeDesktopElements = 1 << 4;

    private const int CFNumberSInt32Type = 3;
    private const uint CFStringEncodingUTF8 = 0x08000100;

    private static readonly IntPtr LayerKey = CreateKey("kCGWindowLayer");
    private static readonly IntPtr OwnerPidKey = CreateKey("kCGWindowOwnerPID");
    private static readonly IntPtr BoundsKey = CreateKey("kCGWindowBounds");

    /// <summary>
    /// Gets the mouse cursor location.
    /// </summary>
    internal static bool TryGetCursorLocation(out double x, out double y)
    {
        x = y = 0;
        var cgEvent = CGEventCreate(IntPtr.Zero);
        if (cgEvent == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var location = CGEventGetLocation(cgEvent);
            (x, y) = (location.X, location.Y);
            return true;
        }
        finally
        {
            CFRelease(cgEvent);
        }
    }

    /// <summary>
    /// Gets the center of the frontmost normal window of another app, i.e. the window the user was working in.
    /// </summary>
    internal static bool TryGetFrontmostWindowCenter(out double x, out double y)
    {
        x = y = 0;
        var windows = CGWindowListCopyWindowInfo(OnScreenOnly | ExcludeDesktopElements, 0);
        if (windows == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var ownPid = Environment.ProcessId;
            var count = CFArrayGetCount(windows);
            // The list is ordered front to back.
            for (nint i = 0; i < count; i++)
            {
                var window = CFArrayGetValueAtIndex(windows, i);
                if (GetInt(window, LayerKey) != 0 || GetInt(window, OwnerPidKey) == ownPid)
                {
                    continue;
                }

                var bounds = CFDictionaryGetValue(window, BoundsKey);
                if (bounds == IntPtr.Zero || !CGRectMakeWithDictionaryRepresentation(bounds, out var rect)
                    || rect.Width <= 1 || rect.Height <= 1)
                {
                    continue;
                }

                x = rect.X + rect.Width / 2;
                y = rect.Y + rect.Height / 2;
                return true;
            }

            return false;
        }
        finally
        {
            CFRelease(windows);
        }
    }

    private static int? GetInt(IntPtr dictionary, IntPtr key)
    {
        var number = CFDictionaryGetValue(dictionary, key);
        return number != IntPtr.Zero && CFNumberGetValue(number, CFNumberSInt32Type, out var value) ? value : null;
    }

    private static IntPtr CreateKey(string name) => CFStringCreateWithCString(IntPtr.Zero, name, CFStringEncodingUTF8);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphics)]
    private static extern CGPoint CGEventGetLocation(IntPtr cgEvent);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out CGRect rect);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out int value);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);
}
