using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Flow.Launcher.Plugin;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    /// <summary>
    /// Listens keyboard globally.
    /// <remarks>Uses WH_KEYBOARD_LL.</remarks>
    /// </summary>
    public unsafe class GlobalHotkey : IDisposable
    {
        private static readonly UnhookWindowsHookExSafeHandle hookId;

        public delegate bool KeyboardCallback(int keyEvent, int vkCode, SpecialKeyState state);
        internal static Func<KeyEvent, int, SpecialKeyState, bool> hookedKeyboardCallback;

        static GlobalHotkey()
        {
            // Set the hook
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;
            hookId = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_KEYBOARD_LL, 
                LowLevelKeyboardProc, 
                PInvoke.GetModuleHandle(curModule.ModuleName), 0);
        }

        public static SpecialKeyState CheckModifiers()
        {
            SpecialKeyState state = new SpecialKeyState();
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0)
            {
                //SHIFT is pressed
                state.ShiftPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CONTROL) & 0x8000) != 0)
            {
                //CONTROL is pressed
                state.CtrlPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_MENU) & 0x8000) != 0)
            {
                //ALT is pressed
                state.AltPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0)
            {
                //WIN is pressed
                state.WinPressed = true;
            }

            return state;
        }

        private static LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
        {
            bool continues = true;

            if (nCode >= 0)
            {
                var wParamValue = (int)wParam.Value;
                if (wParamValue == (int)KeyEvent.WM_KEYDOWN ||
                    wParamValue == (int)KeyEvent.WM_KEYUP ||
                    wParamValue == (int)KeyEvent.WM_SYSKEYDOWN ||
                    wParamValue == (int)KeyEvent.WM_SYSKEYUP)
                {
                    if (hookedKeyboardCallback != null)
                        continues = hookedKeyboardCallback((KeyEvent)wParamValue, Marshal.ReadInt32(lParam), CheckModifiers());
                }
            }

            if (continues)
            {
                return PInvoke.CallNextHookEx(hookId, nCode, wParam, lParam);
            }
            return new LRESULT(-1);
        }

        public void Dispose()
        {
            PInvoke.UnhookWindowsHookEx(new HHOOK(hookId.DangerousGetHandle()));
        }

        ~GlobalHotkey()
        {
            Dispose();
        }
    }
}
