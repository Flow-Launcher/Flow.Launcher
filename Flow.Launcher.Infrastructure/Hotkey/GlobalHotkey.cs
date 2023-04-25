using System;
using System.Runtime.InteropServices;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    /// <summary>
    /// Listens keyboard globally.
    /// <remarks>Uses WH_KEYBOARD_LL.</remarks>
    /// </summary>
    public unsafe class GlobalHotkey : IDisposable
    {
        private static readonly IntPtr hookId;
        
        
        
        public delegate bool KeyboardCallback(KeyEvent keyEvent, int vkCode, SpecialKeyState state);
        internal static Func<KeyEvent, int, SpecialKeyState, bool> hookedKeyboardCallback;

        //Modifier key constants
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_ALT = 0x12;
        private const int VK_WIN = 91;

        static GlobalHotkey()
        {
            // Set the hook
            hookId = InterceptKeys.SetHook(& LowLevelKeyboardProc);
        }

        public static SpecialKeyState CheckModifiers()
        {
            SpecialKeyState state = new SpecialKeyState();
            if ((InterceptKeys.GetKeyState(VK_SHIFT) & 0x8000) != 0)
            {
                //SHIFT is pressed
                state.ShiftPressed = true;
            }
            if ((InterceptKeys.GetKeyState(VK_CONTROL) & 0x8000) != 0)
            {
                //CONTROL is pressed
                state.CtrlPressed = true;
            }
            if ((InterceptKeys.GetKeyState(VK_ALT) & 0x8000) != 0)
            {
                //ALT is pressed
                state.AltPressed = true;
            }
            if ((InterceptKeys.GetKeyState(VK_WIN) & 0x8000) != 0)
            {
                //WIN is pressed
                state.WinPressed = true;
            }

            return state;
        }

        [UnmanagedCallersOnly]
        private static IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
        {
            bool continues = true;

            if (nCode >= 0)
            {
                if (wParam.ToUInt32() == (int)KeyEvent.WM_KEYDOWN ||
                    wParam.ToUInt32() == (int)KeyEvent.WM_KEYUP ||
                    wParam.ToUInt32() == (int)KeyEvent.WM_SYSKEYDOWN ||
                    wParam.ToUInt32() == (int)KeyEvent.WM_SYSKEYUP)
                {
                    if (hookedKeyboardCallback != null)
                        continues = hookedKeyboardCallback((KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), CheckModifiers());
                }
            }

            if (continues)
            {
                return InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam);
            }
            return (IntPtr)(-1);
        }

        public void Dispose()
        {
            InterceptKeys.UnhookWindowsHookEx(hookId);
        }

        ~GlobalHotkey()
        {
            Dispose();
        }
    }
}