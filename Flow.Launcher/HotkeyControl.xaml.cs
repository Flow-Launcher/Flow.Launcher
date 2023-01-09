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

        public HotkeyControl()
        {
            InitializeComponent();
            tbMsgTextOriginal = tbMsg.Text;
            tbMsgForegroundColorOriginal = tbMsg.Foreground;
        }

        private CancellationTokenSource hotkeyUpdateSource;

        private void TbHotkey_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            hotkeyUpdateSource?.Cancel();
            hotkeyUpdateSource?.Dispose();
            hotkeyUpdateSource = new();
            var token = hotkeyUpdateSource.Token;
            e.Handled = true;

            //when alt is pressed, the real key should be e.SystemKey
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            SpecialKeyState specialKeyState = GlobalHotkey.CheckModifiers();

            var hotkeyModel = new HotkeyModel(
                specialKeyState.AltPressed,
                specialKeyState.ShiftPressed,
                specialKeyState.WinPressed,
                specialKeyState.CtrlPressed,
                key);

            if (hotkeyModel.Equals(CurrentHotkey))
            {
                return;
            }

            _ = Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(500, token);
                if (!token.IsCancellationRequested)
                    await SetHotkeyAsync(hotkeyModel);
            });
        }

        public async Task SetHotkeyAsync(HotkeyModel keyModel, bool triggerValidate = true)
        {
            CurrentHotkey = keyModel;

            tbHotkey.Text = CurrentHotkey.ToString();
            tbHotkey.Select(tbHotkey.Text.Length, 0);

            if (triggerValidate)
            {
                CurrentHotkeyAvailable = CurrentHotkey.CharKey != Key.None && CheckHotkeyAvailability();
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

        public Task SetHotkeyAsync(string keyStr, bool triggerValidate = true)
        {
            return SetHotkeyAsync(new HotkeyModel(keyStr), triggerValidate);
        }

        private bool CheckHotkeyAvailability() => HotKeyMapper.CheckAvailability(CurrentHotkey);

        public new bool IsFocused => tbHotkey.IsFocused;

        private void tbHotkey_LostFocus(object sender, RoutedEventArgs e)
        {
            tbMsg.Text = tbMsgTextOriginal;
            tbMsg.SetResourceReference(TextBox.ForegroundProperty, "Color05B");
        }
    }
}
