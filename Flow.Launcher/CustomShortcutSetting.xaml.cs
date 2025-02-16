using Flow.Launcher.Core.Resource;
using System;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.Core;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class CustomShortcutSetting : Window
    {
        private readonly SettingsPaneHotkeyViewModel _hotkeyVm;
        private readonly MainViewModel _mainViewModel;
        public string Key { get; set; } = String.Empty;
        public string Value { get; set; } = String.Empty;
        private string originalKey { get; } = null;
        private string originalValue { get; } = null;
        private bool update { get; } = false;

        public CustomShortcutSetting(SettingsPaneHotkeyViewModel vm, MainViewModel mainVM)
        {
            _hotkeyVm = vm;
            _mainViewModel = mainVM;
            InitializeComponent();
        }

        public CustomShortcutSetting(string key, string value, SettingsPaneHotkeyViewModel vm)
        {
            Key = key;
            Value = value;
            originalKey = key;
            originalValue = value;
            update = true;
            _hotkeyVm = vm;
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
                MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("emptyShortcut"));
                return;
            }
            // Check if key is modified or adding a new one
            if (((update && originalKey != Key) || !update) && _hotkeyVm.DoesShortcutExist(Key))
            {
                MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("duplicateShortcut"));
                return;
            }
            DialogResult = !update || originalKey != Key || originalValue != Value;
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
            _mainViewModel.Show(false);
            Application.Current.MainWindow.Focus();
        }
    }
}
