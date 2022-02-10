using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.QuickSwitch
{
    public static class NativeHelper
    {


        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const uint EVENT_SYSTEM_FOREGROUND = 3;

        [DllImport("user32.dll")]
        public static extern unsafe IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            delegate* unmanaged<IntPtr, uint, IntPtr, int, int, uint, uint, void> lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32")]
        public static extern IntPtr GetForegroundWindow();

        public enum WmType : uint
        {
            WM_KEYDOWN = 0x100,
            WM_KEYUP = 0x101,
            WM_CHAR = 0x102,
            WM_SETTEXT = 0x000C,
            WM_GETTEXT = 0x000D,
            WM_USER = 0x0400,
            CBEM_GETEDITCONTROL = 0x407,
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, WmType Msg, nuint wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, WmType Msg, nuint wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, WmType Msg, nuint wParam, ref nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, WmType Msg, nuint wParam, nint lParam);

    }
}
