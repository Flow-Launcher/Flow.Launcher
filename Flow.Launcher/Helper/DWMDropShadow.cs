using System;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;

namespace Flow.Launcher.Helper;

public class DwmDropShadow
{

    /// <summary>
    /// Drops a standard shadow to a WPF Window, even if the window isborderless. Only works with DWM (Vista and Seven).
    /// This method is much more efficient than setting AllowsTransparency to true and using the DropShadow effect,
    /// as AllowsTransparency involves a huge permormance issue (hardware acceleration is turned off for all the window).
    /// </summary>
    /// <param name="window">Window to which the shadow will be applied</param>
    public static void DropShadowToWindow(Window window)
    {
        if (!DropShadow(window))
        {
            window.SourceInitialized += window_SourceInitialized;
        }
    }

    private static void window_SourceInitialized(object sender, EventArgs e) //fixed typo
    {
        Window window = (Window)sender;

        DropShadow(window);

        window.SourceInitialized -= window_SourceInitialized;
    }

    /// <summary>
    /// The actual method that makes API calls to drop the shadow to the window
    /// </summary>
    /// <param name="window">Window to which the shadow will be applied</param>
    /// <returns>True if the method succeeded, false if not</returns>
    private unsafe static bool DropShadow(Window window)
    {
        try
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            int val = 2;
            int ret1 = PInvoke.DwmSetWindowAttribute(new(helper.Handle), DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY, &val, 4);

            if (ret1 == 0)
            {
                var m = new MARGINS { cyBottomHeight = 0, cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0 };
                int ret2 = PInvoke.DwmExtendFrameIntoClientArea(new(helper.Handle), &m);
                return ret2 == 0;
            }
            else
            {
                return false;
            }
        }
        catch (Exception)
        {
            // Probably dwmapi.dll not found (incompatible OS)
            return false;
        }
    }

}
