using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    public partial class PluginUpdateWindow : Window
    {
        public List<PluginUpdateInfo> Plugins { get; set; } = new();
        public bool Restart { get; set; }

        private readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        public PluginUpdateWindow(List<PluginUpdateInfo> allPlugins)
        {
            Restart = _settings.AutoRestartAfterChanging;
            InitializeComponent();
            foreach (var plugin in allPlugins)
            {
                var checkBox = new CheckBox
                {
                    Content = string.Format(App.API.GetTranslation("updatePluginCheckboxContent"), plugin.Name, plugin.CurrentVersion, plugin.NewVersion),
                    IsChecked = true,
                    Margin = new Thickness(0, 5, 0, 5),
                    Tag = plugin,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                checkBox.Checked += CheckBox_Checked;
                checkBox.Unchecked += CheckBox_Unchecked;
                UpdatePluginStackPanel.Children.Add(checkBox);
                Plugins.Add(plugin);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            if (cb.Tag is not PluginUpdateInfo plugin) return;
            if (!Plugins.Contains(plugin))
            {
                Plugins.Add(plugin);
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            if (cb.Tag is not PluginUpdateInfo plugin) return;
            if (Plugins.Contains(plugin))
            {
                Plugins.Remove(plugin);
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            if (Plugins.Count == 0)
            {
                App.API.ShowMsgBox(App.API.GetTranslation("updatePluginNoSelected"));
                return;
            }

            _ = PluginInstaller.UpdateAllPluginsAsync(Plugins, Restart);

            DialogResult = true;
            Close();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
