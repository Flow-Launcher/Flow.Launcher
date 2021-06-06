﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Shell
{
    public partial class CMDSetting : UserControl
    {
        private readonly Settings _settings;

        public CMDSetting(Settings settings)
        {
            InitializeComponent();
            _settings = settings;
        }

        private void CMDSetting_OnLoaded(object sender, RoutedEventArgs re)
        {
            ReplaceWinR.IsChecked = _settings.ReplaceWinR;
            
            LeaveShellOpen.IsChecked = _settings.LeaveShellOpen;
            
            AlwaysRunAsAdministrator.IsChecked = _settings.RunAsAdministrator;
            
            LeaveShellOpen.IsEnabled = _settings.Shell != Shell.RunCommand;
            
            ShowOnlyMostUsedCMDs.IsChecked = _settings.ShowOnlyMostUsedCMDs;
            
            if ((bool)!ShowOnlyMostUsedCMDs.IsChecked)
                ShowOnlyMostUsedCMDsNumber.IsEnabled = false;

            ShowOnlyMostUsedCMDsNumber.ItemsSource = new List<int>() { 5, 10, 20 };

            if (_settings.ShowOnlyMostUsedCMDsNumber == 0)
            {
                ShowOnlyMostUsedCMDsNumber.SelectedIndex = 0;

                _settings.ShowOnlyMostUsedCMDsNumber = (int)ShowOnlyMostUsedCMDsNumber.SelectedItem;
            }

            LeaveShellOpen.Checked += (o, e) =>
            {
                _settings.LeaveShellOpen = true;
            };

            LeaveShellOpen.Unchecked += (o, e) =>
            {
                _settings.LeaveShellOpen = false;
            };

            AlwaysRunAsAdministrator.Checked += (o, e) =>
            {
                _settings.RunAsAdministrator = true;
            };

            AlwaysRunAsAdministrator.Unchecked += (o, e) =>
            {
                _settings.RunAsAdministrator = false;
            };

            ReplaceWinR.Checked += (o, e) =>
            {
                _settings.ReplaceWinR = true;
            };

            ReplaceWinR.Unchecked += (o, e) =>
            {
                _settings.ReplaceWinR = false;
            };

            ShellComboBox.SelectedIndex = (int) _settings.Shell;
            ShellComboBox.SelectionChanged += (o, e) =>
            {
                _settings.Shell = (Shell) ShellComboBox.SelectedIndex;
                LeaveShellOpen.IsEnabled = _settings.Shell != Shell.RunCommand;
            };

            ShowOnlyMostUsedCMDs.Checked += (o, e) =>
            {
                _settings.ShowOnlyMostUsedCMDs = true;

                ShowOnlyMostUsedCMDsNumber.IsEnabled = true;
            };

            ShowOnlyMostUsedCMDs.Unchecked += (o, e) =>
            {
                _settings.ShowOnlyMostUsedCMDs = false;

                ShowOnlyMostUsedCMDsNumber.IsEnabled = false;
            };

            ShowOnlyMostUsedCMDsNumber.SelectedItem = _settings.ShowOnlyMostUsedCMDsNumber;
            ShowOnlyMostUsedCMDsNumber.SelectionChanged += (o, e) =>
            {
                _settings.ShowOnlyMostUsedCMDsNumber = (int)ShowOnlyMostUsedCMDsNumber.SelectedItem;
            };

        }
    }
}
