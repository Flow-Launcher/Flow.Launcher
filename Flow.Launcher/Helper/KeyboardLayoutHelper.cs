using System;
using System.Runtime.InteropServices;
using System.Text;

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

    [DllImport("kernel32.dll")]
    private static extern int GetLocaleInfoA(uint Locale, uint LCType, StringBuilder lpLCData, int cchData);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    // Used to get the language name of the keyboard layout
    private const uint LOCALE_SLANGUAGE = 0x00000002;

    // The string to search for in the language name of a keyboard layout
    private const string LAYOUT_ENGLISH_SEARCH = "english";

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

            // Get language name for the layout
            var sb = new StringBuilder(256);
            GetLocaleInfoA(langId, LOCALE_SLANGUAGE, sb, sb.Capacity);
            var langName = sb.ToString().ToLowerInvariant();

            // Check if it's an English layout
            if (langName.Contains(LAYOUT_ENGLISH_SEARCH))
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
