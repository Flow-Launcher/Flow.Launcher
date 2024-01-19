using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using PropertyChanged;

namespace Flow.Launcher.WelcomePages
{
    [DoNotNotify]
    public partial class WelcomePage2 : UserControl
    {
        private Settings Settings { get; set; }

        private Brush tbMsgForegroundColorOriginal;

        private string tbMsgTextOriginal;

        public WelcomePage2()
        {
            InitializeComponent();
            hotkeyControl.SetHotkeyAsync(Settings.Hotkey, false);
        }

        private void HotkeyControl_OnGotFocus(object sender, RoutedEventArgs args)
        {
            HotKeyMapper.RemoveHotkey(Settings.Hotkey);
        }

        private void HotkeyControl_OnLostFocus(object sender, RoutedEventArgs args)
        {
            if (hotkeyControl.CurrentHotkeyAvailable)
            {
                HotKeyMapper.SetHotkey(hotkeyControl.CurrentHotkey, HotKeyMapper.OnToggleHotkey);
                Settings.Hotkey = hotkeyControl.CurrentHotkey.ToString();
            }
            else
            {
                HotKeyMapper.SetHotkey(new HotkeyModel(Settings.Hotkey), HotKeyMapper.OnToggleHotkey);
            }
        }
    }
}
