using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using System.Threading;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher
{
    public partial class HotkeyControl : UserControl
    {
        private Brush tbMsgForegroundColorOriginal;

        private string tbMsgTextOriginal;

        public HotkeyModel CurrentHotkey { get; private set; }
        public bool CurrentHotkeyAvailable { get; private set; }

        public event EventHandler HotkeyChanged;

        protected virtual void OnHotkeyChanged() => HotkeyChanged?.Invoke(this, EventArgs.Empty);

        private Func<int, int, SpecialKeyState, bool> callback { get; set; }

        public HotkeyControl()
        {
            InitializeComponent();
            tbMsgTextOriginal = tbMsg.Text;
            tbMsgForegroundColorOriginal = tbMsg.Foreground;

            callback = TbHotkey_OnPreviewKeyDown;

            GotFocus += (_, _) =>
            {
                PluginManager.API.RegisterGlobalKeyboardCallback(callback);
            };
            LostFocus += (_, _) =>
            {
                PluginManager.API.RemoveGlobalKeyboardCallback(callback);
                state.AltPressed = false;
                state.CtrlPressed = false;
                state.ShiftPressed = false;
                state.WinPressed = false;
            };
        }

        private CancellationTokenSource hotkeyUpdateSource;

        private SpecialKeyState state = new();

        private bool TbHotkey_OnPreviewKeyDown(int keyevent, int vkcode, SpecialKeyState dummy)
        {
            var key = KeyInterop.KeyFromVirtualKey(vkcode);

            if ((KeyEvent)keyevent is not (KeyEvent.WM_KEYDOWN or KeyEvent.WM_SYSKEYDOWN))
            {
                switch (key)
                {
                    case Key.LeftAlt or Key.RightAlt:
                        state.AltPressed = false;
                        break;
                    case Key.LeftCtrl or Key.RightCtrl:
                        state.CtrlPressed = false;
                        break;
                    case Key.LeftShift or Key.RightShift:
                        state.ShiftPressed = false;
                        break;
                    case Key.LWin or Key.LWin:
                        state.WinPressed = false;
                        break;
                    default:
                        break;
                }
                return true;
            }

            switch (key)
            {
                case Key.LeftAlt or Key.RightAlt:
                    state.AltPressed = true;
                    break;
                case Key.LeftCtrl or Key.RightCtrl:
                    state.CtrlPressed = true;
                    break;
                case Key.LeftShift or Key.RightShift:
                    state.ShiftPressed = true;
                    break;
                case Key.LWin or Key.LWin:
                    state.WinPressed = true;
                    break;
            }


            hotkeyUpdateSource?.Cancel();
            hotkeyUpdateSource?.Dispose();
            hotkeyUpdateSource = new();
            var token = hotkeyUpdateSource.Token;


            var hotkeyModel = new HotkeyModel(
                state.AltPressed,
                state.ShiftPressed,
                state.WinPressed,
                state.CtrlPressed,
                key);

            var hotkeyString = hotkeyModel.ToString();

            if (hotkeyString == tbHotkey.Text)
            {
                return false;
            }
            Log.Debug("test hotkey" + hotkeyString);

            _ = Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(500, token);
                if (!token.IsCancellationRequested)
                    await SetHotkey(hotkeyModel);
            });

            return false;
        }

        public async Task SetHotkey(HotkeyModel keyModel, bool triggerValidate = true)
        {
            CurrentHotkey = keyModel;

            tbHotkey.Text = CurrentHotkey.ToString();
            tbHotkey.Select(tbHotkey.Text.Length, 0);

            if (triggerValidate)
            {
                CurrentHotkeyAvailable = CheckHotkeyAvailability();
                if (!CurrentHotkeyAvailable)
                {
                    tbMsg.Foreground = new SolidColorBrush(Colors.Red);
                    tbMsg.Text = InternationalizationManager.Instance.GetTranslation("hotkeyUnavailable");
                }
                else
                {
                    tbMsg.Foreground = new SolidColorBrush(Colors.Green);
                    tbMsg.Text = InternationalizationManager.Instance.GetTranslation("success");
                }
                tbMsg.Visibility = Visibility.Visible;
                OnHotkeyChanged();

                var token = hotkeyUpdateSource.Token;
                await Task.Delay(500, token);
                if (token.IsCancellationRequested)
                    return;
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
                Keyboard.ClearFocus();
            }
        }

        public void SetHotkey(string keyStr, bool triggerValidate = true)
        {
            SetHotkey(new HotkeyModel(keyStr), triggerValidate);
        }

        private bool CheckHotkeyAvailability() => HotKeyMapper.CheckAvailability(CurrentHotkey);

        public new bool IsFocused => tbHotkey.IsFocused;

        private void tbHotkey_LostFocus(object sender, RoutedEventArgs e)
        {
            tbMsg.Text = tbMsgTextOriginal;
            tbMsg.Foreground = tbMsgForegroundColorOriginal;
        }
    }
}