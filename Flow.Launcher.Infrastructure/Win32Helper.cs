using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace Flow.Launcher.Infrastructure
{
    public static class Win32Helper
    {
        #region Blur Handling

        /*
        Found on https://github.com/riverar/sample-win10-aeroglass
        */

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_INVALID_STATE = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        /// <summary>
        /// Checks if the blur theme is enabled
        /// </summary>
        public static bool IsBlurTheme()
        {
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                var resource = Application.Current.TryFindResource("ThemeBlurEnabled");

                if (resource is bool b)
                    return b;

                return false;
            }

            return false;
        }

        /// <summary>
        /// Sets the blur for a window via SetWindowCompositionAttribute
        /// </summary>
        public static void SetBlurForWindow(Window w, bool blur)
        {
            SetWindowAccent(w, blur ? AccentState.ACCENT_ENABLE_BLURBEHIND : AccentState.ACCENT_DISABLED);
        }

        private static void SetWindowAccent(Window w, AccentState state)
        {
            var windowHelper = new WindowInteropHelper(w);

            windowHelper.EnsureHandle();

            var accent = new AccentPolicy { AccentState = state };
            var accentStructSize = Marshal.SizeOf(accent);

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }
        #endregion
    }
}
