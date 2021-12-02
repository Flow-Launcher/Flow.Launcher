using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Flow.Launcher.Resources.Pages
{
    /// <summary>
    /// WelcomePage2.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WelcomePage2 : Page
    {
        private readonly Settings settings;

        public WelcomePage2(Settings settings)
        {
            InitializeComponent();
            this.settings = settings;
            HotkeyControl.SetHotkey(new Infrastructure.Hotkey.HotkeyModel(settings.Hotkey));
            HotkeyControl.HotkeyChanged += (_, _) =>
            {
                if (HotkeyControl.CurrentHotkeyAvailable)
                {
                    HotKeyMapper.SetHotkey(HotkeyControl.CurrentHotkey, HotKeyMapper.OnToggleHotkey);
                    HotKeyMapper.RemoveHotkey(settings.Hotkey);
                    settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
                }
            };
        }
    }
}
