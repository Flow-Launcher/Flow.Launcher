using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;

namespace Flow.Launcher
{
    public partial class CustomShortcutSetting : Window
    {
        private SettingWindow _settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;
        private Settings _settings;

        public string Key { get; set; }
        public string Value { get; set; }
        public ShortCutModel ShortCut => (Key, Value);
        public CustomShortcutSetting()
        {
            InitializeComponent();
        }

        public CustomShortcutSetting((string, string) shortcut)
        {
            (Key, Value) = shortcut;
            InitializeComponent();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
