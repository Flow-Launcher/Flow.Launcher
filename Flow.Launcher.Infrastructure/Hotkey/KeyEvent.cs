using Windows.Win32;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    public enum KeyEvent
    {
        /// <summary>
        /// Key down
        /// </summary>
        WM_KEYDOWN = (int)PInvoke.WM_KEYDOWN,

        /// <summary>
        /// Key up
        /// </summary>
        WM_KEYUP = (int)PInvoke.WM_KEYUP,

        /// <summary>
        /// System key up
        /// </summary>
        WM_SYSKEYUP = (int)PInvoke.WM_SYSKEYUP,

        /// <summary>
        /// System key down
        /// </summary>
        WM_SYSKEYDOWN = (int)PInvoke.WM_SYSKEYDOWN
    }
}
