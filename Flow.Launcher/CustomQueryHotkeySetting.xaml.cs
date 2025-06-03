using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    public partial class CustomQueryHotkeySetting : Window
    {
        private readonly Settings _settings;

        private bool update;
        private CustomPluginHotkey updateCustomHotkey;

        public CustomQueryHotkeySetting(Settings settings)
        {
            _settings = settings;
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
                _settings.CustomPluginHotkeys ??= new ObservableCollection<CustomPluginHotkey>();

                var pluginHotkey = new CustomPluginHotkey
                {
                    Hotkey = HotkeyControl.CurrentHotkey.ToString(), ActionKeyword = tbAction.Text
                };
                _settings.CustomPluginHotkeys.Add(pluginHotkey);

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
            updateCustomHotkey = _settings.CustomPluginHotkeys.FirstOrDefault(o =>
                o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
            if (updateCustomHotkey == null)
            {
                App.API.ShowMsgBox(App.API.GetTranslation("invalidPluginHotkey"));
                Close();
                return;
            }

            tbAction.Text = updateCustomHotkey.ActionKeyword;
            HotkeyControl.SetHotkey(updateCustomHotkey.Hotkey, false);
            update = true;
            lblAdd.Text = App.API.GetTranslation("update");
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            App.API.ChangeQuery(tbAction.Text);
            App.API.ShowMainWindow();
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
