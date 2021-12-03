using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Windows.Navigation;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage2
    {
        private Settings Settings { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else
                throw new ArgumentException("Unexpected Parameter setting.");
            InitializeComponent();

            HotkeyControl.SetHotkey(new Infrastructure.Hotkey.HotkeyModel(Settings.Hotkey));
            HotkeyControl.HotkeyChanged += (_, _) =>
            {
                if (HotkeyControl.CurrentHotkeyAvailable)
                {
                    HotKeyMapper.SetHotkey(HotkeyControl.CurrentHotkey, HotKeyMapper.OnToggleHotkey);
                    HotKeyMapper.RemoveHotkey(Settings.Hotkey);
                    Settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
                }
            };
        }
    }
}
