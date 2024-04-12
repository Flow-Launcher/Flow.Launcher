using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
using NHotkey;

namespace Flow.Launcher
{
    public partial class HotkeyControl : UserControl, IDisposable, INotifyPropertyChanged
    {
        public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
            nameof(Hotkey),
            typeof(string),
            typeof(HotkeyControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
        );
        public HotkeyModel CurrentHotkey { get; private set; }
        public bool CurrentHotkeyAvailable { get; private set; }

        public string Hotkey
        {
            get => (string)GetValue(HotkeyProperty);
            set
            {
                SetValue(HotkeyProperty, value);
                OnPropertyChanged(nameof(KeysToDisplay));
            }
        }

        public string DefaultHotkey { get; set; }

        public string[] KeysToDisplay => Hotkey.Split(" + ");

        private bool _isEditingHotkey = false;
        public bool IsEditingHotkey {
            get => _isEditingHotkey;
            set {
                _isEditingHotkey = value;
                OnPropertyChanged();
            }
        }

        #nullable enable
        public EventHandler<HotkeyEventArgs>? Action { get; set; }
        #nullable restore

        public event EventHandler HotkeyChanged;

        /// <summary>
        /// Designed for Preview Hotkey and KeyGesture.
        /// </summary>
        public bool ValidateKeyGesture { get; set; } = false;

        protected virtual void OnHotkeyChanged() => HotkeyChanged?.Invoke(this, EventArgs.Empty);

        public HotkeyControl()
        {
            InitializeComponent();
            Loaded += HotkeyControl_Loaded;
        }

        private void HotkeyControl_LostFocus(object o, RoutedEventArgs routedEventArgs)
        {
            HotKeyMapper.SetHotkey(CurrentHotkey, Action);
        }

        private void HotkeyControl_GotFocus(object o, RoutedEventArgs routedEventArgs)
        {
            HotKeyMapper.RemoveHotkey(Hotkey);
        }

        private void HotkeyControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = SetHotkeyAsync(Hotkey, false);

            if (Action is not null)
            {
                GotFocus += HotkeyControl_GotFocus;
                LostFocus += HotkeyControl_LostFocus;
            }
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
            // tbHotkey.Text = keyModel.ToString();
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
                    Hotkey = keyModel.ToString();
                    // To trigger LostFocus
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
                    Keyboard.ClearFocus();
                }
            }
            else
            {
                CurrentHotkey = keyModel;
                Hotkey = keyModel.ToString();
            }
        }

        public Task SetHotkeyAsync(string keyStr, bool triggerValidate = true)
        {
            return SetHotkeyAsync(new HotkeyModel(keyStr), triggerValidate);
        }

        private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) => hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);

        // public new bool IsFocused => tbHotkey.IsFocused;

        private void tbHotkey_LostFocus(object sender, RoutedEventArgs e)
        {
            // tbHotkey.Text = CurrentHotkey?.ToString() ?? "";
            // tbHotkey.Select(tbHotkey.Text.Length, 0);
        }

        private void tbHotkey_GotFocus(object sender, RoutedEventArgs e)
        {
            ResetMessage();
        }

        private void ResetMessage()
        {
            tbMsg.Text = InternationalizationManager.Instance.GetTranslation("flowlauncherPressHotkey");
            tbMsg.SetResourceReference(TextBox.ForegroundProperty, "Color05B");
        }

        private void SetMessage(bool hotkeyAvailable)
        {
            if (!hotkeyAvailable)
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
        }

        public void Dispose()
        {
            hotkeyUpdateSource?.Dispose();
            Loaded -= HotkeyControl_Loaded;
            GotFocus -= HotkeyControl_GotFocus;
            LostFocus -= HotkeyControl_LostFocus;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnStopRecordingClicked(object sender, RoutedEventArgs e)
        {
            IsEditingHotkey = false;
        }

        private void OnResetToDefaultClicked(object sender, RoutedEventArgs e)
        {
            IsEditingHotkey = false;
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = DefaultHotkey;
            HotKeyMapper.SetHotkey(new HotkeyModel(Hotkey), Action);
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            IsEditingHotkey = false;
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = "";
        }
    }
}
