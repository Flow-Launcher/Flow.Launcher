using System;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    public partial class CustomShortcutSetting : Window
    {
        private SettingWindow _settingWidow;
        private bool update;
        private Settings _settings;

        public string Key { get; set; }
        public string Value { get; set; }
        public CustomShortcutModel ShortCut => (Key, Value);

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
            if (_settings.CustomShortcuts.Contains(new CustomShortcutModel(Key, Value)))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("dulplicateShortcut"));
                DialogResult = false;
            }
            else if (String.IsNullOrEmpty(Key) || String.IsNullOrEmpty(Value))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidShortcut"));
                DialogResult = false;
            }
            Close();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
