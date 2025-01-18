using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Core;

namespace Flow.Launcher
{
    /// <summary>
    /// Interaction Logic of PriorityChangeWindow.xaml
    /// </summary>
    public partial class PriorityChangeWindow : Window
    {
        private readonly PluginPair plugin;
        private readonly Internationalization translater = InternationalizationManager.Instance;
        private readonly PluginViewModel pluginViewModel;
        public PriorityChangeWindow(string pluginId, PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            plugin = PluginManager.GetPluginForId(pluginId);
            this.pluginViewModel = pluginViewModel;
            if (plugin == null)
            {
                MessageBoxEx.Show(translater.GetTranslation("cannotFindSpecifiedPlugin"));
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
                string msg = translater.GetTranslation("invalidPriority");
                MessageBoxEx.Show(msg);
            }

        }

        private void PriorityChangeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            tbAction.Text = pluginViewModel.Priority.ToString();
            tbAction.Focus();
        }
        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            TextBox textBox = Keyboard.FocusedElement as TextBox;
            if (textBox != null)
            {
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                textBox.MoveFocus(tRequest);
            }
        }
    }
}
