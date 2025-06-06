using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Windows.Win32;

internal static partial class PInvoke
{
    // SetWindowLong
    // Edited from: https://github.com/files-community/Files

    [DllImport("User32", EntryPoint = "SetWindowLongW", ExactSpelling = true)]
    private static extern int _SetWindowLong(HWND hWnd, int nIndex, int dwNewLong);

    [DllImport("User32", EntryPoint = "SetWindowLongPtrW", ExactSpelling = true)]
    private static extern nint _SetWindowLongPtr(HWND hWnd, int nIndex, nint dwNewLong);

    // NOTE:
    //  CsWin32 doesn't generate SetWindowLong on other than x86 and vice versa.
    //  For more info, visit https://github.com/microsoft/CsWin32/issues/882
    public static unsafe nint SetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong)
    {
        return sizeof(nint) is 4
          ? _SetWindowLong(hWnd, (int)nIndex, (int)dwNewLong)
          : _SetWindowLongPtr(hWnd, (int)nIndex, dwNewLong);
    }

    // GetWindowLong

    [DllImport("User32", EntryPoint = "GetWindowLongW", ExactSpelling = true)]
    private static extern int _GetWindowLong(HWND hWnd, int nIndex);

    [DllImport("User32", EntryPoint = "GetWindowLongPtrW", ExactSpelling = true)]
    private static extern nint _GetWindowLongPtr(HWND hWnd, int nIndex);

    // NOTE:
    //  CsWin32 doesn't generate GetWindowLong on other than x86 and vice versa.
    //  For more info, visit https://github.com/microsoft/CsWin32/issues/882
    public static unsafe nint GetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
    {
        return sizeof(nint) is 4
          ? _GetWindowLong(hWnd, (int)nIndex)
          : _GetWindowLongPtr(hWnd, (int)nIndex);
    }
}
