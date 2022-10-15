using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher
{
    public partial class CustomShortcutSetting : Window
    {
        private Settings _settings;
        private bool update = false;
        public string Key { get; set; }
        public string Value { get; set; }
        public CustomShortcutModel ShortCut => (Key, Value);

        public CustomShortcutSetting(Settings settings)
        {
            _settings = settings;
            InitializeComponent();
        }

        public CustomShortcutSetting(CustomShortcutModel shortcut, Settings settings)
        {
            Key = shortcut.Key;
            Value = shortcut.Value;
            _settings = settings;
            update = true;
            InitializeComponent();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(Key) || String.IsNullOrEmpty(Value))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("emptyShortcut"));
                return;
            }
            if (!update && (_settings.CustomShortcuts.Contains(new CustomShortcutModel(Key, Value)) || _settings.BuiltinShortcuts.Contains(new BuiltinShortcutModel(Key, Value, null))))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("dulplicateShortcut"));
                return;
            }
            DialogResult = true;
            Close();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnTestShortcut_OnClick(object sender, RoutedEventArgs e)
        {
            App.API.ChangeQuery(tbExpand.Text);
            Application.Current.MainWindow.Show();
            Application.Current.MainWindow.Opacity = 1;
            Application.Current.MainWindow.Focus();
        }
    }
}
