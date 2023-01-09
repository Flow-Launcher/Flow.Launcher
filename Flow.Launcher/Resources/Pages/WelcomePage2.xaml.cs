using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage2
    {
        private Settings Settings { get; set; }

        private Brush tbMsgForegroundColorOriginal;

        private string tbMsgTextOriginal;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else
                throw new ArgumentException("Unexpected Parameter setting.");
            
            InitializeComponent();
            tbMsgTextOriginal = HotkeyControl.tbMsg.Text;
            tbMsgForegroundColorOriginal = HotkeyControl.tbMsg.Foreground;

            HotkeyControl.SetHotkeyAsync(Settings.Hotkey, false);
        }
        private void HotkeyControl_OnGotFocus(object sender, RoutedEventArgs args)
        {
            HotKeyMapper.RemoveHotkey(Settings.Hotkey);
        }
        private void HotkeyControl_OnLostFocus(object sender, RoutedEventArgs args)
        {
            if (HotkeyControl.CurrentHotkeyAvailable)
            {
                HotKeyMapper.SetHotkey(HotkeyControl.CurrentHotkey, HotKeyMapper.OnToggleHotkey);
                Settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
            }
            else
            {
                HotKeyMapper.SetHotkey(new HotkeyModel(Settings.Hotkey), HotKeyMapper.OnToggleHotkey);
            }

            HotkeyControl.tbMsg.Text = tbMsgTextOriginal;
            HotkeyControl.tbMsg.Foreground = tbMsgForegroundColorOriginal;
        }
    }
}
