﻿using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher
{
    public partial class CustomShortcutSetting : Window
    {
        public Settings Settings { get; set; } = Ioc.Default.GetRequiredService<Settings>();

        private readonly SettingsPaneHotkeyViewModel _hotkeyVm;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        private string originalKey { get; } = null;
        private string originalValue { get; } = null;
        private bool update { get; } = false;

        public CustomShortcutSetting(SettingsPaneHotkeyViewModel vm)
        {
            _hotkeyVm = vm;
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
            if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(Value))
            {
                App.API.ShowMsgBox(App.API.GetTranslation("emptyShortcut"));
                return;
            }
            // Check if key is modified or adding a new one
            if (((update && originalKey != Key) || !update) && _hotkeyVm.DoesShortcutExist(Key))
            {
                App.API.ShowMsgBox(App.API.GetTranslation("duplicateShortcut"));
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
            App.API.ShowMainWindow();
            Application.Current.MainWindow.Focus();
        }
    }
}
