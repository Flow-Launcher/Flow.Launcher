using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Linq;
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
        public CustomShortcutModel ShortCut;

        public CustomShortcutSetting(Settings settings)
        {
            _settings = settings;
            InitializeComponent();
        }

        public CustomShortcutSetting(CustomShortcutModel shortcut, Settings settings)
        {
            Key = shortcut.Key;
            Value = shortcut.Value;
            ShortCut = shortcut;
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
            bool modified = false;
            if (String.IsNullOrEmpty(Key) || String.IsNullOrEmpty(Value))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("emptyShortcut"));
                return;
            }
            if (!update)
            {
                ShortCut = new CustomShortcutModel(Key, Value);
                if (_settings.CustomShortcuts.Any(x => x.Key == Key) || _settings.BuiltinShortcuts.Any(x => x.Key == Key))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("duplicateShortcut"));
                    return;
                }
                modified = true;
            }
            else
            {
                if (ShortCut.Key != Key && _settings.CustomShortcuts.Any(x => x.Key == Key) || _settings.BuiltinShortcuts.Any(x => x.Key == Key))
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("duplicateShortcut"));
                    return;
                }
                modified = ShortCut.Key != Key || ShortCut.Value != Value;
            }
            DialogResult = modified;
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
