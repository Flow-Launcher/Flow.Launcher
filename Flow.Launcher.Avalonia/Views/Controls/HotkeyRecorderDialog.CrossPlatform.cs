using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using WpfKey = System.Windows.Input.Key;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class HotkeyRecorderDialog
    {
        private TopLevel? _keyCaptureRoot;

        partial void AttachKeyboardHook()
        {
            // The dialog lives in the window's overlay layer; capture at the window so keys are seen wherever focus is.
            _keyCaptureRoot = TopLevel.GetTopLevel(this);
            _keyCaptureRoot?.AddHandler(KeyDownEvent, OnRecorderKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            _keyCaptureRoot?.AddHandler(KeyUpEvent, OnRecorderKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        partial void DetachKeyboardHook()
        {
            _keyCaptureRoot?.RemoveHandler(KeyDownEvent, OnRecorderKeyDown);
            _keyCaptureRoot?.RemoveHandler(KeyUpEvent, OnRecorderKeyUp);
            _keyCaptureRoot = null;
        }

        private void OnRecorderKeyDown(object? sender, KeyEventArgs e)
        {
            // Swallow every key while recording, like the Windows hook, so Escape/Enter/Space can be recorded too.
            e.Handled = true;

            var key = MacKeyMap.GetKey(e.PhysicalKey);
            var modifiers = e.KeyModifiers;

            // Don't treat modifiers as main keys
            switch (key)
            {
                case WpfKey.LeftAlt or WpfKey.RightAlt:
                    modifiers |= KeyModifiers.Alt;
                    key = WpfKey.None;
                    break;
                case WpfKey.LeftCtrl or WpfKey.RightCtrl:
                    modifiers |= KeyModifiers.Control;
                    key = WpfKey.None;
                    break;
                case WpfKey.LeftShift or WpfKey.RightShift:
                    modifiers |= KeyModifiers.Shift;
                    key = WpfKey.None;
                    break;
                case WpfKey.LWin or WpfKey.RWin:
                    modifiers |= KeyModifiers.Meta;
                    key = WpfKey.None;
                    break;
            }

            var model = new HotkeyModel(
                modifiers.HasFlag(KeyModifiers.Alt),
                modifiers.HasFlag(KeyModifiers.Shift),
                modifiers.HasFlag(KeyModifiers.Meta),
                modifiers.HasFlag(KeyModifiers.Control),
                key);

            ApplyRecordedHotkey(model);
        }

        private static void OnRecorderKeyUp(object? sender, KeyEventArgs e)
        {
            // Keep the dialog's own Escape/Enter handling from closing it mid-recording.
            e.Handled = true;
        }
    }
}
