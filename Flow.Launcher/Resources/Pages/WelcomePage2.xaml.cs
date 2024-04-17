using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.ViewModel;

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

            HotkeyControl.ChangeHotkey = ChangeHotkeyCommand;
        }

        [RelayCommand]
        public void ChangeHotkey(HotkeyModel hotkeyModel)
        {
            Settings.Hotkey = hotkeyModel.ToString();
            HotKeyMapper.SetHotkey(hotkeyModel, HotKeyMapper.OnToggleHotkey);
        }

        private void HotkeyControl_OnGotFocus(object sender, RoutedEventArgs args)
        {
            HotKeyMapper.RemoveHotkey(Settings.Hotkey);
        }

        private void HotkeyControl_OnLostFocus(object sender, RoutedEventArgs args)
        {
            HotkeyControl.tbMsg.Text = tbMsgTextOriginal;
            HotkeyControl.tbMsg.Foreground = tbMsgForegroundColorOriginal;
        }
    }
}
