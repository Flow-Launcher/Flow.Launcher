using Avalonia.Controls;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using System;
using InfrastructureGlobalHotkey = Flow.Launcher.Infrastructure.Hotkey.GlobalHotkey;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class HotkeyRecorderDialog
    {
        private bool _altDown;
        private bool _ctrlDown;
        private bool _shiftDown;
        private bool _winDown;
        private Func<KeyEvent, int, SpecialKeyState, bool>? _previousKeyboardCallback;

        partial void AttachKeyboardHook()
        {
            // Initialize Global Hotkey Hook when dialog opens
            try 
            {
                // Sync initial modifier state
                var state = InfrastructureGlobalHotkey.CheckModifiers();
                _altDown = state.AltPressed;
                _ctrlDown = state.CtrlPressed;
                _shiftDown = state.ShiftPressed;
                _winDown = state.WinPressed;

                _previousKeyboardCallback = InfrastructureGlobalHotkey.hookedKeyboardCallback;
                InfrastructureGlobalHotkey.hookedKeyboardCallback = GlobalKeyHook;
            }
            catch (Exception ex)
            {
                Log.Exception(nameof(HotkeyRecorderDialog), "Failed to attach global keyboard hook", ex);
            }
        }

        partial void DetachKeyboardHook()
        {
            if (InfrastructureGlobalHotkey.hookedKeyboardCallback == GlobalKeyHook)
            {
                InfrastructureGlobalHotkey.hookedKeyboardCallback = _previousKeyboardCallback;
            }
        }

        private bool GlobalKeyHook(KeyEvent keyEvent, int vkCode, SpecialKeyState state)
        {
            var wpfKey = InfrastructureGlobalHotkey.GetKeyFromVk(vkCode);
            bool isKeyDown = (keyEvent == KeyEvent.WM_KEYDOWN || keyEvent == KeyEvent.WM_SYSKEYDOWN);
            bool isKeyUp = (keyEvent == KeyEvent.WM_KEYUP || keyEvent == KeyEvent.WM_SYSKEYUP);

            // Track modifier state manually (since we swallow keys, OS state is stale)
            if (wpfKey == System.Windows.Input.Key.LeftAlt || wpfKey == System.Windows.Input.Key.RightAlt)
                _altDown = isKeyDown;
            else if (wpfKey == System.Windows.Input.Key.LeftCtrl || wpfKey == System.Windows.Input.Key.RightCtrl)
                _ctrlDown = isKeyDown;
            else if (wpfKey == System.Windows.Input.Key.LeftShift || wpfKey == System.Windows.Input.Key.RightShift)
                _shiftDown = isKeyDown;
            else if (wpfKey == System.Windows.Input.Key.LWin || wpfKey == System.Windows.Input.Key.RWin)
                _winDown = isKeyDown;

            // Only process key down events for UI updates
            if (isKeyDown)
            {
                // Capture current modifier state for the UI thread
                bool alt = _altDown;
                bool ctrl = _ctrlDown;
                bool shift = _shiftDown;
                bool win = _winDown;

                // Marshal to UI Thread
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    HandleGlobalKey(vkCode, alt, ctrl, shift, win);
                });
            }

            // Return false to SWALLOW the key (prevents other apps from receiving it)
            return false; 
        }

        private void HandleGlobalKey(int vkCode, bool altDown, bool ctrlDown, bool shiftDown, bool winDown)
        {
            var wpfKey = InfrastructureGlobalHotkey.GetKeyFromVk(vkCode);

            // Don't treat modifiers as main keys
            if (wpfKey == System.Windows.Input.Key.LeftAlt || wpfKey == System.Windows.Input.Key.RightAlt ||
                wpfKey == System.Windows.Input.Key.LeftCtrl || wpfKey == System.Windows.Input.Key.RightCtrl ||
                wpfKey == System.Windows.Input.Key.LeftShift || wpfKey == System.Windows.Input.Key.RightShift ||
                wpfKey == System.Windows.Input.Key.LWin || wpfKey == System.Windows.Input.Key.RWin)
            {
                wpfKey = System.Windows.Input.Key.None;
            }

            var model = new HotkeyModel(
                altDown,
                shiftDown,
                winDown,
                ctrlDown,
                wpfKey);

            UpdateKeysDisplay(model);
            
            // Update Save button enablement based on validity and availability
            var isValid = model.Validate();
            var isAvailable = isValid && HotKeyMapper.CheckAvailability(model);
            
            IsPrimaryButtonEnabled = isAvailable;

            var alert = this.FindControl<Border>("Alert");
            var tbMsg = this.FindControl<TextBlock>("tbMsg");
            if (alert != null && tbMsg != null)
            {
                if (isValid && !isAvailable)
                {
                    // TODO: Get actual translation
                    tbMsg.Text = "Hotkey already in use";
                    alert.IsVisible = true;
                }
                else
                {
                    alert.IsVisible = false;
                }
            }
        }
    }
}
