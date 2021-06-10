using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Flow.Launcher
{
    /// <summary>
    /// Interaction Logic of PriorityChangeWindow.xaml
    /// </summary>
    public partial class PriorityChangeWindow : Window
    {
        private readonly PluginPair plugin;
        private Settings settings;
        private readonly II18N translater = InternationalizationManager.Instance;
        private readonly PluginViewModel pluginViewModel;

        public PriorityChangeWindow(string pluginId, Settings settings, PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            plugin = PluginManager.GetPluginForId(pluginId);
            this.settings = settings;
            this.pluginViewModel = pluginViewModel;
            if (plugin == null)
            {
                MessageBox.Show(translater.GetTranslation("cannotFindSpecifiedPlugin"));
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
                MessageBox.Show(msg);
            }

        }

        private void PriorityChangeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OldPriority.Text = pluginViewModel.Priority.ToString();
            tbAction.Focus();
        }
    }
}