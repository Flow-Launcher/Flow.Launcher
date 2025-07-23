using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace Flow.Launcher.Plugin.SharedModels;

/// <summary>
/// Contains full information about a cursor.
/// </summary>
/// <remarks>
/// Use this class to replace the System.Windows.Forms.Cursor class which can cause possible System.PlatformNotSupportedException.
/// </remarks>
public class CursorInfo
{
    /// <summary>
    /// Gets the current position of the cursor in screen coordinates.
    /// </summary>
    public static Point Position
    {
        get
        {
            if (!PInvoke.GetCursorPos(out var pt))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }
            return pt;
        }
    }
}
