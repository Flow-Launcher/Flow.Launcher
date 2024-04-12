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
using Flow.Launcher.ViewModel;
using NHotkey;

namespace Flow.Launcher
{
    public partial class HotkeyControl : UserControl, INotifyPropertyChanged
    {
        // /// <summary>
        // /// Designed for Preview Hotkey and KeyGesture.
        // /// </summary>
        // public static readonly DependencyProperty ValidateKeyGestureProperty = DependencyProperty.Register(
        //     nameof(ValidateKeyGesture), typeof(bool), typeof(HotkeyControlViewModel),
        //     new PropertyMetadata(default(bool)));
        //
        // public bool ValidateKeyGesture
        // {
        //     get { return (bool)GetValue(ValidateKeyGestureProperty); }
        //     set { SetValue(ValidateKeyGestureProperty, value); }
        // }
        //
        // public static readonly DependencyProperty DefaultHotkeyProperty = DependencyProperty.Register(
        //     nameof(DefaultHotkey), typeof(string), typeof(HotkeyControl), new PropertyMetadata(default(string)));
        //
        // public string DefaultHotkey
        // {
        //     get { return (string)GetValue(DefaultHotkeyProperty); }
        //     set { SetValue(DefaultHotkeyProperty, value); }
        // }

        public HotkeyControl()
        {
            InitializeComponent();
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

            ((HotkeyControlViewModel)this.DataContext).KeyDown(hotkeyModel);
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
