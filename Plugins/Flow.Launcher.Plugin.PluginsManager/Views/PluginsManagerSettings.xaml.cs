﻿using Flow.Launcher.Plugin.PluginsManager.ViewModels;

namespace Flow.Launcher.Plugin.PluginsManager.Views
{
    /// <summary>
    /// Interaction logic for PluginsManagerSettings.xaml
    /// </summary>
    public partial class PluginsManagerSettings
    {
        internal PluginsManagerSettings(SettingsViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}
