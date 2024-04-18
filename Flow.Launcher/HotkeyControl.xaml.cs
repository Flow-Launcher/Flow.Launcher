using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using JetBrains.Annotations;

#nullable enable

namespace Flow.Launcher
{
    public partial class HotkeyControl : INotifyPropertyChanged
    {
        /// <summary>
        /// Designed for Preview Hotkey and KeyGesture.
        /// </summary>
        public static readonly DependencyProperty ValidateKeyGestureProperty = DependencyProperty.Register(
            nameof(ValidateKeyGesture), typeof(bool), typeof(HotkeyControl),
            new PropertyMetadata(default(bool)));

        public bool ValidateKeyGesture
        {
            get { return (bool)GetValue(ValidateKeyGestureProperty); }
            set { SetValue(ValidateKeyGestureProperty, value); }
        }

        public static readonly DependencyProperty DefaultHotkeyProperty = DependencyProperty.Register(
            nameof(DefaultHotkey), typeof(string), typeof(HotkeyControl), new PropertyMetadata(default(string)));

        public string DefaultHotkey
        {
            get { return (string)GetValue(DefaultHotkeyProperty); }
            set { SetValue(DefaultHotkeyProperty, value); }
        }

        private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HotkeyControl hotkeyControl)
            {
                return;
            }

            hotkeyControl.SetKeysToDisplay(new HotkeyModel(hotkeyControl.Hotkey));
            hotkeyControl.CurrentHotkey = new HotkeyModel(hotkeyControl.Hotkey);
        }


        public static readonly DependencyProperty ChangeHotkeyProperty = DependencyProperty.Register(
            nameof(ChangeHotkey), typeof(ICommand), typeof(HotkeyControl), new PropertyMetadata(default(ICommand)));

        public ICommand? ChangeHotkey
        {
            get { return (ICommand)GetValue(ChangeHotkeyProperty); }
            set { SetValue(ChangeHotkeyProperty, value); }
        }


        public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
            nameof(Hotkey), typeof(string), typeof(HotkeyControl),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

        public string Hotkey
        {
            get { return (string)GetValue(HotkeyProperty); }
            set { SetValue(HotkeyProperty, value); }
        }

        public HotkeyControl()
        {
            InitializeComponent();

            HotkeyList.ItemsSource = KeysToDisplay;
            SetKeysToDisplay(CurrentHotkey);
        }

        /*------------------ New Logic Structure Part------------------------*/

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (HotkeyBtn.IsChecked != true)
                return;
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

            CurrentHotkey = hotkeyModel;
            SetKeysToDisplay(CurrentHotkey);
        }


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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
            hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);

        public string? Message { get; set; }
        public bool MessageVisibility { get; set; }
        public SolidColorBrush? MessageColor { get; set; }


        public bool CurrentHotkeyAvailable { get; private set; }


        private void SetMessage(string messageKey, bool error)
        {
            Message = InternationalizationManager.Instance.GetTranslation(messageKey);
            MessageColor = error ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            MessageVisibility = true;
        }


        private string EmptyHotkeyKey = "none";
        public string EmptyHotkey => InternationalizationManager.Instance.GetTranslation(EmptyHotkeyKey);
        private const string KeySeparator = " + ";

        public ObservableCollection<string> KeysToDisplay { get; set; } = new ObservableCollection<string>();

        public bool IsEmpty => KeysToDisplay.Count == 0 || (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyHotkey);

        public HotkeyModel CurrentHotkey { get; private set; } = new(false, false, false, false, Key.None);


        public void StartRecording()
        {
            if (!HotkeyBtn.IsChecked ?? false)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Hotkey))
            {
                HotKeyMapper.RemoveHotkey(Hotkey);
            }
            /* 1. Key Recording Start */
            /* 2. Key Display area clear
             * 3. Key Display when typing*/
        }

        private void StopRecording()
        {
            try
            {
                var converter = new KeyGestureConverter();
                _ = (KeyGesture)converter.ConvertFromString(CurrentHotkey.ToString())!;
            }
            catch (Exception e) when (e is NotSupportedException or InvalidEnumArgumentException)
            {
                SetMessage("Hotkey Invalid", true);
                CurrentHotkey = new HotkeyModel(Hotkey);
                SetKeysToDisplay(CurrentHotkey);
                return;
            }

            HotkeyBtn.IsChecked = false;

            SetHotkey(CurrentHotkey, true);
        }

        private void ResetToDefault()
        {
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = DefaultHotkey;
            CurrentHotkey = new HotkeyModel(Hotkey);

            SetKeysToDisplay(CurrentHotkey);

            SetHotkey(CurrentHotkey);

            HotkeyBtn.IsChecked = false;
        }


        private void SetHotkey(HotkeyModel keyModel, bool triggerValidate = true)
        {
            // tbHotkey.Text = keyModel.ToString();
            // tbHotkey.Select(tbHotkey.Text.Length, 0);

            if (triggerValidate)
            {
                bool hotkeyAvailable = CheckHotkeyAvailability(keyModel, ValidateKeyGesture);
                SetMessage(hotkeyAvailable ? "success" : "hotkeyUnavailable", !hotkeyAvailable);

                if (!hotkeyAvailable)
                {
                    return;
                }

                Hotkey = keyModel.ToString();
                SetKeysToDisplay(CurrentHotkey);
                ChangeHotkey?.Execute(keyModel);
            }
            else
            {
                Hotkey = keyModel.ToString();
                ChangeHotkey?.Execute(keyModel);
            }
        }

        public void Delete()
        {
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = "";
            SetKeysToDisplay(new HotkeyModel(false, false, false, false, Key.None));
            HotkeyBtn.IsChecked = false;
        }

        private void SetKeysToDisplay(HotkeyModel? hotkey)
        {
            KeysToDisplay.Clear();

            if (hotkey == null || hotkey == default(HotkeyModel))
            {
                KeysToDisplay.Add(EmptyHotkey);
                return;
            }

            foreach (var key in hotkey.Value.EnumerateDisplayKeys()!)
            {
                KeysToDisplay.Add(key);
            }
        }

        public void SetHotkey(string? keyStr, bool triggerValidate = true)
        {
            SetHotkey(new HotkeyModel(keyStr), triggerValidate);
        }


        private void HotkeyBtn_OnChecked(object sender, RoutedEventArgs e) => StartRecording();


        private void ResetButton_OnClick(object sender, RoutedEventArgs e) => ResetToDefault();
        private void DeleteBtn_OnClick(object sender, RoutedEventArgs e) => Delete();

        private void StopRecordingBtn_Click(object sender, RoutedEventArgs e) => StopRecording();
    }
}
