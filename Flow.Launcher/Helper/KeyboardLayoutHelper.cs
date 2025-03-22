using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Helper;

public static class KeyboardLayoutHelper
{
    #region Windows API

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    // https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/70feba9f-294e-491e-b6eb-56532684c37f
    private static readonly uint[] EnglishLanguageIds =
    {
        0x0009, 0x0409, 0x0809, 0x0C09, 0x1000, 0x1009, 0x1409, 0x1809, 0x1C09, 0x2009, 0x2409, 0x2809, 0x2C09,
        0x3009, 0x3409, 0x3C09, 0x4009, 0x4409, 0x4809, 0x4C09,
    };

    private static IntPtr FindEnglishKeyboardLayout()
    {
        // Get the number of keyboard layouts
        var count = GetKeyboardLayoutList(0, null);
        if (count <= 0) return IntPtr.Zero;

        // Get all keyboard layouts
        var keyboardLayouts = new IntPtr[count];
        GetKeyboardLayoutList(count, keyboardLayouts);

        // Look for any English keyboard layout
        foreach (var layout in keyboardLayouts)
        {
            // The lower word contains the language identifier
            var langId = (uint)layout.ToInt32() & 0xFFFF;

            // Check if it's an English layout
            if (EnglishLanguageIds.Contains(langId))
            {
                return layout;
            }
        }

        return IntPtr.Zero;
    }

    #endregion

    // Query textbox keyboard layout
    private static IntPtr _previousLayout;

    public static void SetEnglishKeyboardLayout()
    {
        // Find an installed English layout
        var englishLayout = FindEnglishKeyboardLayout();

        // No installed English layout found
        if (englishLayout == IntPtr.Zero) return;

        var hwnd = GetForegroundWindow();
        var threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

        // Store current keyboard layout
        _previousLayout = GetKeyboardLayout(threadId) & 0xFFFF;

        // Switch to English layout
        ActivateKeyboardLayout(englishLayout, 0);
    }

    public static void SetPreviousKeyboardLayout()
    {
        if (_previousLayout == IntPtr.Zero) return;
        ActivateKeyboardLayout(_previousLayout, 0);
        _previousLayout = IntPtr.Zero;
    }
}
