using System.Collections.Generic;
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

            CloseShellAfterPress.IsChecked = _settings.CloseShellAfterPress;
            
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

            CloseShellAfterPress.Checked += (o, e) =>
            {
                _settings.CloseShellAfterPress = true;
                LeaveShellOpen.IsChecked = false;
                LeaveShellOpen.IsEnabled = false;
            };

            CloseShellAfterPress.Unchecked += (o, e) =>
            {
                _settings.CloseShellAfterPress = false;
                LeaveShellOpen.IsEnabled = true;
            };

            LeaveShellOpen.Checked += (o, e) =>
            {
                _settings.LeaveShellOpen = true;
                CloseShellAfterPress.IsChecked = false;
                CloseShellAfterPress.IsEnabled = false;
            };

            LeaveShellOpen.Unchecked += (o, e) =>
            {
                _settings.LeaveShellOpen = false;
                CloseShellAfterPress.IsEnabled = true;
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

            ShellComboBox.SelectedIndex = _settings.Shell switch
            {
                Shell.Cmd => 0,
                Shell.Powershell => 1,
                Shell.Pwsh => 2,
                _ => ShellComboBox.Items.Count - 1
            };

            ShellComboBox.SelectionChanged += (o, e) =>
            {
                _settings.Shell = ShellComboBox.SelectedIndex switch
                {
                    0 => Shell.Cmd,
                    1 => Shell.Powershell,
                    2 => Shell.Pwsh,
                    _ => Shell.RunCommand
                };
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
