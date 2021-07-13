using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher
{
    public partial class CustomQueryHotkeySetting : Window
    {
        private SettingWindow _settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;
        private Settings _settings;

        public CustomQueryHotkeySetting(SettingWindow settingWidow, Settings settings)
        {
            _settingWidow = settingWidow;
            InitializeComponent();
            _settings = settings;
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            if (!update)
            {
                if (!ctlHotkey.CurrentHotkeyAvailable)
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("hotkeyIsNotUnavailable"));
                    return;
                }

                if (_settings.CustomPluginHotkeys == null)
                {
                    _settings.CustomPluginHotkeys = new ObservableCollection<CustomPluginHotkey>();
                }

                var pluginHotkey = new CustomPluginHotkey
                {
                    Hotkey = ctlHotkey.CurrentHotkey.ToString(),
                    ActionKeyword = tbAction.Text
                };
                _settings.CustomPluginHotkeys.Add(pluginHotkey);

                HotKeyMapper.SetCustomQueryHotkey(pluginHotkey);
            }
            else
            {
                if (updateCustomHotkey.Hotkey != ctlHotkey.CurrentHotkey.ToString() && !ctlHotkey.CurrentHotkeyAvailable)
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("hotkeyIsNotUnavailable"));
                    return;
                }
                var oldHotkey = updateCustomHotkey.Hotkey;
                updateCustomHotkey.ActionKeyword = tbAction.Text;
                updateCustomHotkey.Hotkey = ctlHotkey.CurrentHotkey.ToString();
                //remove origin hotkey
                HotKeyMapper.RemoveHotkey(oldHotkey);
                HotKeyMapper.SetCustomQueryHotkey(updateCustomHotkey);
            }

            Close();
        }

        public void UpdateItem(CustomPluginHotkey item)
        {
            updateCustomHotkey = _settings.CustomPluginHotkeys.FirstOrDefault(o => o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
            if (updateCustomHotkey == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidPluginHotkey"));
                Close();
                return;
            }

            tbAction.Text = updateCustomHotkey.ActionKeyword;
            ctlHotkey.SetHotkey(updateCustomHotkey.Hotkey, false);
            update = true;
            lblAdd.Text = InternationalizationManager.Instance.GetTranslation("update");
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            App.API.ChangeQuery(tbAction.Text);
            Application.Current.MainWindow.Visibility = Visibility.Visible;
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
    }
}
