using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Rounds the corners of a blurred macOS window. Avalonia draws AcrylicBlur with a behind-window NSVisualEffectView
/// that fills the whole rectangular NSWindow over an opaque window background, so both show past the rounded window
/// border. The blur view's layer gets the corner radius (layer clipping also clips the behind-window blur) and the
/// window background is cleared.
/// </summary>
internal static class MacWindowShape
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    // NSVisualEffectBlendingModeBehindWindow; Avalonia's title bar material uses WithinWindow and must stay unclipped.
    private const nint BlendingModeBehindWindow = 0;

    // CACornerMask; the effect view is unflipped, so MinY is the bottom edge.
    private const nuint BottomLeftCorner = 1 << 0;
    private const nuint BottomRightCorner = 1 << 1;
    private const nuint TopLeftCorner = 1 << 2;
    private const nuint TopRightCorner = 1 << 3;

    private static readonly IntPtr NSVisualEffectViewClass = objc_getClass("NSVisualEffectView");
    private static readonly IntPtr NSColorClass = objc_getClass("NSColor");
    private static readonly IntPtr ContentViewSel = sel_registerName("contentView");
    private static readonly IntPtr SubviewsSel = sel_registerName("subviews");
    private static readonly IntPtr CountSel = sel_registerName("count");
    private static readonly IntPtr ObjectAtIndexSel = sel_registerName("objectAtIndex:");
    private static readonly IntPtr IsKindOfClassSel = sel_registerName("isKindOfClass:");
    private static readonly IntPtr BlendingModeSel = sel_registerName("blendingMode");
    private static readonly IntPtr LayerSel = sel_registerName("layer");
    private static readonly IntPtr SetCornerRadiusSel = sel_registerName("setCornerRadius:");
    private static readonly IntPtr SetMaskedCornersSel = sel_registerName("setMaskedCorners:");
    private static readonly IntPtr SetMasksToBoundsSel = sel_registerName("setMasksToBounds:");
    private static readonly IntPtr ClearColorSel = sel_registerName("clearColor");
    private static readonly IntPtr SetBackgroundColorSel = sel_registerName("setBackgroundColor:");

    /// <summary>
    /// Clips the window's behind-window blur to <paramref name="cornerRadius"/> (in points). A CALayer has a single
    /// radius, so corners with radius 0 stay square and the others use the largest radius.
    /// </summary>
    internal static void SetCornerRadius(Window window, CornerRadius cornerRadius)
    {
        var nsWindow = GetNSWindow(window);
        if (nsWindow == IntPtr.Zero)
        {
            return;
        }

        var radius = Math.Max(Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight),
            Math.Max(cornerRadius.BottomRight, cornerRadius.BottomLeft));
        var corners = (cornerRadius.TopLeft > 0 ? TopLeftCorner : 0)
            | (cornerRadius.TopRight > 0 ? TopRightCorner : 0)
            | (cornerRadius.BottomRight > 0 ? BottomRightCorner : 0)
            | (cornerRadius.BottomLeft > 0 ? BottomLeftCorner : 0);

        // AutoFitContentView: the blur view is a layer-backed direct subview, next to the title bar material.
        var subviews = objc_msgSend(objc_msgSend(nsWindow, ContentViewSel), SubviewsSel);
        var count = objc_msgSend_nuint(subviews, CountSel);
        for (nuint i = 0; i < count; i++)
        {
            var view = objc_msgSend_ptr_nuint(subviews, ObjectAtIndexSel, i);
            if (!objc_msgSend_bool_ptr(view, IsKindOfClassSel, NSVisualEffectViewClass)
                || objc_msgSend_nint(view, BlendingModeSel) != BlendingModeBehindWindow)
            {
                continue;
            }

            var layer = objc_msgSend(view, LayerSel);
            if (layer == IntPtr.Zero)
            {
                continue;
            }

            objc_msgSend_void_double(layer, SetCornerRadiusSel, Math.Max(radius, 0));
            objc_msgSend_void_nuint(layer, SetMaskedCornersSel, corners);
            objc_msgSend_void_bool(layer, SetMasksToBoundsSel, radius > 0);
        }
    }

    /// <summary>
    /// Clears the window background, which Avalonia sets to the opaque windowBackgroundColor whenever it applies a blur
    /// transparency level; without this the square background shows outside the clipped blur. Does nothing while the
    /// window is opaque.
    /// </summary>
    internal static void ClearBackgroundIfTransparent(Window window)
    {
        var nsWindow = GetNSWindow(window);
        if (nsWindow != IntPtr.Zero && window.ActualTransparencyLevel != WindowTransparencyLevel.None)
        {
            objc_msgSend_void_ptr(nsWindow, SetBackgroundColorSel, objc_msgSend(NSColorClass, ClearColorSel));
        }
    }

    private static IntPtr GetNSWindow(Window window) =>
        window.TryGetPlatformHandle() is { HandleDescriptor: "NSWindow", Handle: var nsWindow } ? nsWindow : IntPtr.Zero;

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC)]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr_nuint(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_ptr(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nuint objc_msgSend_nuint(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_ptr(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_double(IntPtr receiver, IntPtr selector, double value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nuint(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);
}
