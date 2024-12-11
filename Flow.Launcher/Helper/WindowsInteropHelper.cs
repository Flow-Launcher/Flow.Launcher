using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Point = System.Windows.Point;

namespace Flow.Launcher.Helper;

public class WindowsInteropHelper
{
    private static HWND _hwnd_shell;
    private static HWND _hwnd_desktop;

    //Accessors for shell and desktop handlers
    //Will set the variables once and then will return them
    private static HWND HWND_SHELL
    {
        get
        {
            return _hwnd_shell != HWND.Null ? _hwnd_shell : _hwnd_shell = PInvoke.GetShellWindow();
        }
    }

    private static HWND HWND_DESKTOP
    {
        get
        {
            return _hwnd_desktop != HWND.Null ? _hwnd_desktop : _hwnd_desktop = PInvoke.GetDesktopWindow();
        }
    }

    const string WINDOW_CLASS_CONSOLE = "ConsoleWindowClass";
    const string WINDOW_CLASS_WINTAB = "Flip3D";
    const string WINDOW_CLASS_PROGMAN = "Progman";
    const string WINDOW_CLASS_WORKERW = "WorkerW";

    public unsafe static bool IsWindowFullscreen()
    {
        //get current active window
        var hWnd = PInvoke.GetForegroundWindow();

        if (hWnd.Equals(HWND.Null))
        {
            return false;
        }

        //if current active window is desktop or shell, exit early
        if (hWnd.Equals(HWND_DESKTOP) || hWnd.Equals(HWND_SHELL))
        {
            return false;
        }

        string windowClass;
        int capacity = 256;
        char[] buffer = new char[capacity];
        fixed (char* pBuffer = buffer)
        {
            PInvoke.GetClassName(hWnd, pBuffer, capacity);
            int validLength = Array.IndexOf(buffer, '\0');
            if (validLength < 0) validLength = capacity;
            windowClass = new string(buffer, 0, validLength);
        }

        //for Win+Tab (Flip3D)
        if (windowClass == WINDOW_CLASS_WINTAB)
        {
            return false;
        }

        PInvoke.GetWindowRect(hWnd, out var appBounds);

        //for console (ConsoleWindowClass), we have to check for negative dimensions
        if (windowClass == WINDOW_CLASS_CONSOLE)
        {
            return appBounds.top < 0 && appBounds.bottom < 0;
        }

        //for desktop (Progman or WorkerW, depends on the system), we have to check
        if (windowClass is WINDOW_CLASS_PROGMAN or WINDOW_CLASS_WORKERW)
        {
            var hWndDesktop = PInvoke.FindWindowEx(hWnd, HWND.Null, "SHELLDLL_DefView", null);
            hWndDesktop = PInvoke.FindWindowEx(hWndDesktop, HWND.Null, "SysListView32", "FolderView");
            if (!hWndDesktop.Equals(IntPtr.Zero))
            {
                return false;
            }
        }

        Rectangle screenBounds = Screen.FromHandle(hWnd).Bounds;
        return (appBounds.bottom - appBounds.top) == screenBounds.Height && (appBounds.right - appBounds.left) == screenBounds.Width;
    }

    /// <summary>
    ///     disable windows toolbar's control box
    ///     this will also disable system menu with Alt+Space hotkey
    /// </summary>
    public static void DisableControlBox(Window win)
    {
        var hwnd = new HWND(new WindowInteropHelper(win).Handle);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE) & ~(int)WINDOW_STYLE.WS_SYSMENU);
    }

    /// <summary>
    /// Transforms pixels to Device Independent Pixels used by WPF
    /// </summary>
    /// <param name="visual">current window, required to get presentation source</param>
    /// <param name="unitX">horizontal position in pixels</param>
    /// <param name="unitY">vertical position in pixels</param>
    /// <returns>point containing device independent pixels</returns>
    public static Point TransformPixelsToDIP(Visual visual, double unitX, double unitY)
    {
        Matrix matrix;
        var source = PresentationSource.FromVisual(visual);
        if (source is not null)
        {
            matrix = source.CompositionTarget.TransformFromDevice;
        }
        else
        {
            using var src = new HwndSource(new HwndSourceParameters());
            matrix = src.CompositionTarget.TransformFromDevice;
        }
        return new Point((int)(matrix.M11 * unitX), (int)(matrix.M22 * unitY));
    }
}
