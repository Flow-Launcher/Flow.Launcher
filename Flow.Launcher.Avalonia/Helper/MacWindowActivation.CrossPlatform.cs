using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// NSApplication/NSWindow calls a hotkey-driven launcher needs on macOS: becoming the active app while another app is
/// frontmost, opening on the current Space, and handing focus back when the launcher hides.
/// </summary>
internal static class MacWindowActivation
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    // NSWindowCollectionBehavior
    private const nuint MoveToActiveSpace = 1 << 1;
    private const nuint FullScreenAuxiliary = 1 << 8;

    private static readonly IntPtr NSApplicationClass = objc_getClass("NSApplication");
    private static readonly IntPtr SharedApplicationSel = sel_registerName("sharedApplication");
    private static readonly IntPtr ActivateIgnoringOtherAppsSel = sel_registerName("activateIgnoringOtherApps:");
    private static readonly IntPtr IsActiveSel = sel_registerName("isActive");
    private static readonly IntPtr HideSel = sel_registerName("hide:");
    private static readonly IntPtr CollectionBehaviorSel = sel_registerName("collectionBehavior");
    private static readonly IntPtr SetCollectionBehaviorSel = sel_registerName("setCollectionBehavior:");

    private static IntPtr SharedApplication => objc_msgSend(NSApplicationClass, SharedApplicationSel);

    // NSWindow whose collection behavior was already configured; avoids two ObjC round-trips on every show.
    private static IntPtr _configuredWindow;

    /// <summary>
    /// Makes the window open on the active Space (also over full-screen apps) and activates the app, so the window
    /// becomes key even though another app is frontmost. Call before showing the window.
    /// </summary>
    internal static void ActivateForWindow(Window window)
    {
        if (window.TryGetPlatformHandle() is { HandleDescriptor: "NSWindow", Handle: var nsWindow }
            && nsWindow != IntPtr.Zero && nsWindow != _configuredWindow)
        {
            var behavior = objc_msgSend_nuint(nsWindow, CollectionBehaviorSel);
            var wanted = behavior | MoveToActiveSpace | FullScreenAuxiliary;
            if (wanted != behavior)
            {
                objc_msgSend_void_nuint(nsWindow, SetCollectionBehaviorSel, wanted);
            }

            _configuredWindow = nsWindow;
        }

        objc_msgSend_void_bool(SharedApplication, ActivateIgnoringOtherAppsSel, true);
    }

    /// <summary>
    /// Hides the app when it is still active, so macOS gives focus back to the previously active app.
    /// </summary>
    internal static void ReturnFocusToPreviousApp()
    {
        var app = SharedApplication;
        if (objc_msgSend_bool(app, IsActiveSel))
        {
            objc_msgSend_void_ptr(app, HideSel, IntPtr.Zero);
        }
    }

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC)]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nuint objc_msgSend_nuint(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nuint(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_ptr(IntPtr receiver, IntPtr selector, IntPtr value);
}
