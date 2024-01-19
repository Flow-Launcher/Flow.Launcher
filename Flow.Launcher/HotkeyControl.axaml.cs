using System;
using System.Threading.Tasks;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using System.Threading;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PropertyChanged;
using FocusManager = Avalonia.Input.FocusManager;
using Key = Avalonia.Input.Key;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Flow.Launcher
{
    [DoNotNotify]
    public partial class HotkeyControl : UserControl
    {
        public HotkeyModel CurrentHotkey { get; private set; }
        public bool CurrentHotkeyAvailable { get; private set; }

        public event EventHandler HotkeyChanged;

        /// <summary>
        /// Designed for Preview Hotkey and KeyGesture.
        /// </summary>
        public bool ValidateKeyGesture { get; set; } = false;

        protected virtual void OnHotkeyChanged() => HotkeyChanged?.Invoke(this, EventArgs.Empty);

        public HotkeyControl()
        {
            InitializeComponent();
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
            Key key = e.Key;

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

            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(500, token);
                if (!token.IsCancellationRequested)
                    await SetHotkeyAsync(hotkeyModel);
            });
        }

        public async Task SetHotkeyAsync(HotkeyModel keyModel, bool triggerValidate = true)
        {
            tbHotkey.Text = keyModel.ToString();
            // tbHotkey.Select(tbHotkey.Text.Length, 0);

            if (triggerValidate)
            {
                bool hotkeyAvailable = CheckHotkeyAvailability(keyModel, ValidateKeyGesture);
                CurrentHotkeyAvailable = hotkeyAvailable;
                SetMessage(hotkeyAvailable);
                OnHotkeyChanged();

                var token = hotkeyUpdateSource.Token;
                await Task.Delay(500, token);
                if (token.IsCancellationRequested)
                    return;

                if (CurrentHotkeyAvailable)
                {
                    CurrentHotkey = keyModel;
                    // To trigger LostFocus
                    Keyboard.ClearFocus();
                }
            }
            else
            {
                CurrentHotkey = keyModel;
            }
        }

        public Task SetHotkeyAsync(string keyStr, bool triggerValidate = true)
        {
            return SetHotkeyAsync(new HotkeyModel(keyStr), triggerValidate);
        }

        private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
            hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);

        public new bool IsFocused => tbHotkey.IsFocused;

        private void tbHotkey_LostFocus(object sender, RoutedEventArgs e)
        {
            tbHotkey.Text = CurrentHotkey?.ToString() ?? "";
            // tbHotkey.Select(tbHotkey.Text.Length, 0);
        }

        private void tbHotkey_GotFocus(object sender, RoutedEventArgs e)
        {
            ResetMessage();
        }

        private void ResetMessage()
        {
            tbMsg.Text = InternationalizationManager.Instance.GetTranslation("flowlauncherPressHotkey");
            // tbMsg.SetResourceReference(TextBox.ForegroundProperty, "Color05B");
        }

        private void SetMessage(bool hotkeyAvailable)
        {
            if (!hotkeyAvailable)
            {
                // tbMsg.Foreground = new SolidColorBrush(Colors.Red);
                tbMsg.Text = InternationalizationManager.Instance.GetTranslation("hotkeyUnavailable");
            }
            else
            {
                // tbMsg.Foreground = new SolidColorBrush(Colors.Green);
                tbMsg.Text = InternationalizationManager.Instance.GetTranslation("success");
            }

            // tbMsg.Visibility = Visibility.Visible;
        }
    }
}
