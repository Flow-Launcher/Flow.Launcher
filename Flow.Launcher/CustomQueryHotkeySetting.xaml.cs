using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Flow.Launcher.Core;

namespace Flow.Launcher
{
    public partial class CustomQueryHotkeySetting : Window
    {
        private SettingWindow _settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;
        public Settings Settings { get; }

        public CustomQueryHotkeySetting(SettingWindow settingWidow, Settings settings)
        {
            _settingWidow = settingWidow;
            Settings = settings;
            InitializeComponent();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            if (!update)
            {
                Settings.CustomPluginHotkeys ??= new ObservableCollection<CustomPluginHotkey>();

                var pluginHotkey = new CustomPluginHotkey
                {
                    Hotkey = HotkeyControl.CurrentHotkey.ToString(), ActionKeyword = tbAction.Text
                };
                Settings.CustomPluginHotkeys.Add(pluginHotkey);

                HotKeyMapper.SetCustomQueryHotkey(pluginHotkey);
            }
            else
            {
                var oldHotkey = updateCustomHotkey.Hotkey;
                updateCustomHotkey.ActionKeyword = tbAction.Text;
                updateCustomHotkey.Hotkey = HotkeyControl.CurrentHotkey.ToString();
                //remove origin hotkey
                HotKeyMapper.RemoveHotkey(oldHotkey);
                HotKeyMapper.SetCustomQueryHotkey(updateCustomHotkey);
            }

            Close();
        }


        public void UpdateItem(CustomPluginHotkey item)
        {
            updateCustomHotkey = Settings.CustomPluginHotkeys.FirstOrDefault(o =>
                o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
            if (updateCustomHotkey == null)
            {
                MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("invalidPluginHotkey"));
                Close();
                return;
            }

            tbAction.Text = updateCustomHotkey.ActionKeyword;
            HotkeyControl.SetHotkey(updateCustomHotkey.Hotkey, false);
            update = true;
            lblAdd.Text = InternationalizationManager.Instance.GetTranslation("update");
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            App.API.ChangeQuery(tbAction.Text);
            Application.Current.MainWindow.Show();
            Application.Current.MainWindow.Opacity = 1;
            Application.Current.MainWindow.Focus();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            if (Keyboard.FocusedElement is not TextBox textBox) return;

            TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            textBox.MoveFocus(tRequest);
        }
    }
}
