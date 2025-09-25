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
        private static readonly HOOKPROC _procKeyboard = HookKeyboardCallback;
        private static readonly UnhookWindowsHookExSafeHandle hookId;

        public delegate bool KeyboardCallback(KeyEvent keyEvent, int vkCode, SpecialKeyState state);
        internal static Func<KeyEvent, int, SpecialKeyState, bool> hookedKeyboardCallback;

        static GlobalHotkey()
        {
            // Set the hook
            hookId = SetHook(_procKeyboard, WINDOWS_HOOK_ID.WH_KEYBOARD_LL);
        }

        private static UnhookWindowsHookExSafeHandle SetHook(HOOKPROC proc, WINDOWS_HOOK_ID hookId)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return PInvoke.SetWindowsHookEx(hookId, proc, PInvoke.GetModuleHandle(curModule.ModuleName), 0);
        }

        public static SpecialKeyState CheckModifiers()
        {
            SpecialKeyState state = new SpecialKeyState();
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LSHIFT) & 0x8000) != 0)
            {
                //SHIFT is pressed
                state.LeftShiftPressed = true;
                state.ShiftPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RSHIFT) & 0x8000) != 0)
            {
                //SHIFT is pressed
                state.RightShiftPressed = true;
                state.ShiftPressed = true;
            }
            
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LCONTROL) & 0x8000) != 0)
            {
                //CONTROL is pressed
                state.LeftCtrlPressed = true;
                state.CtrlPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RCONTROL) & 0x8000) != 0)
            {
                //CONTROL is pressed
                state.RightCtrlPressed = true;
                state.CtrlPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LMENU) & 0x8000) != 0)
            {
                //ALT is pressed
                state.LeftAltPressed = true;
                state.AltPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RMENU) & 0x8000) != 0)
            {
                //ALT is pressed
                state.RightAltPressed = true;
                state.AltPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0)
            {
                //WIN is pressed
                state.LWinPressed = true;
                state.WinPressed = true;
            }
            if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RWIN) & 0x8000) != 0)
            {
                //WIN is pressed
                state.RWinPressed = true;
                state.WinPressed = true;
            }

            return state;
        }

        private static LRESULT HookKeyboardCallback(int nCode, WPARAM wParam, LPARAM lParam)
        {
            bool continues = true;

            if (nCode >= 0)
            {
                if (wParam.Value == (int)KeyEvent.WM_KEYDOWN ||
                    wParam.Value == (int)KeyEvent.WM_KEYUP ||
                    wParam.Value == (int)KeyEvent.WM_SYSKEYDOWN ||
                    wParam.Value == (int)KeyEvent.WM_SYSKEYUP)
                {
                    if (hookedKeyboardCallback != null)
                        continues = hookedKeyboardCallback((KeyEvent)wParam.Value, Marshal.ReadInt32(lParam), CheckModifiers());
                }
            }

            if (continues)
            {
                return PInvoke.CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            return new LRESULT(1);
        }

        public void Dispose()
        {
            hookId.Dispose();
        }

        ~GlobalHotkey()
        {
            Dispose();
        }
    }
}
