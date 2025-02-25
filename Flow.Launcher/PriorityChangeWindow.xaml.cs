using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flow.Launcher
{
    /// <summary>
    /// Interaction Logic of PriorityChangeWindow.xaml
    /// </summary>
    public partial class PriorityChangeWindow : Window
    {
        private readonly PluginPair plugin;
        private readonly PluginViewModel pluginViewModel;
        public PriorityChangeWindow(string pluginId, PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            plugin = PluginManager.GetPluginForId(pluginId);
            this.pluginViewModel = pluginViewModel;
            if (plugin == null)
            {
                App.API.ShowMsgBox(App.API.GetTranslation("cannotFindSpecifiedPlugin"));
                Close();
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(tbAction.Text.Trim(), out var newPriority))
            {
                pluginViewModel.ChangePriority(newPriority);
                Close();
            }
            else
            {
                string msg = App.API.GetTranslation("invalidPriority");
                App.API.ShowMsgBox(msg);
            }
        }

        private void PriorityChangeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            tbAction.Text = pluginViewModel.Priority.ToString();
            tbAction.Focus();
        }

        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            if (Keyboard.FocusedElement is TextBox textBox)
            {
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                textBox.MoveFocus(tRequest);
            }
        }
    }
}
