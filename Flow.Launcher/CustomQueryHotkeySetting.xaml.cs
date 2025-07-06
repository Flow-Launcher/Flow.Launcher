using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    public partial class CustomQueryHotkeySetting : Window
    {
        public string Hotkey { get; set; } = string.Empty;
        public string ActionKeyword { get; set; } = string.Empty;

        private readonly bool update;
        private readonly CustomPluginHotkey originalCustomHotkey;

        public CustomQueryHotkeySetting()
        {
            InitializeComponent();
            lblAdd.Visibility = Visibility.Visible;
        }

        public CustomQueryHotkeySetting(CustomPluginHotkey hotkey)
        {
            originalCustomHotkey = hotkey;
            update = true;
            ActionKeyword = originalCustomHotkey.ActionKeyword;
            InitializeComponent();
            lblUpdate.Visibility = Visibility.Visible;
            HotkeyControl.SetHotkey(originalCustomHotkey.Hotkey, false);
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            Hotkey = HotkeyControl.CurrentHotkey.ToString();

            if (string.IsNullOrEmpty(Hotkey) && string.IsNullOrEmpty(ActionKeyword))
            {
                App.API.ShowMsgBox(App.API.GetTranslation("emptyPluginHotkey"));
                return;
            }

            DialogResult = !update || originalCustomHotkey.Hotkey != Hotkey || originalCustomHotkey.ActionKeyword != ActionKeyword;
            Close();
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            App.API.ChangeQuery(tbAction.Text);
            App.API.ShowMainWindow();
            Application.Current.MainWindow.Focus();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
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
