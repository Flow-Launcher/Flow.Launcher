using System;
using System.Collections.Generic;
using System.Windows;

namespace Flow.Launcher.Plugin.Program
{
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;
        public Settings _settings;
        public Dictionary<string, bool> SuffixesStatus => _settings.BuiltinSuffixesStatus;
        public Dictionary<string, bool> ProtocolsStatus => _settings.BuiltinProtocolsStatus;

        public ProgramSuffixes(PluginInitContext context, Settings settings)
        {
            this.context = context;
            _settings = settings;
            InitializeComponent();
            tbSuffixes.Text = string.Join(Settings.SuffixSeperator, _settings.CustomSuffixes);
            tbProtocols.Text = string.Join(Settings.SuffixSeperator, _settings.CustomProtocols);
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            var suffixes = tbSuffixes.Text.Split(Settings.SuffixSeperator, StringSplitOptions.RemoveEmptyEntries);
            var protocols = tbProtocols.Text.Split(Settings.SuffixSeperator, StringSplitOptions.RemoveEmptyEntries);

            if (suffixes.Length == 0 && _settings.UseCustomSuffixes)
            {
                string warning = context.API.GetTranslation("flowlauncher_plugin_program_suffixes_cannot_empty");
                MessageBox.Show(warning);
                return;
            }

            if (protocols.Length == 0 && _settings.UseCustomProtocols)
            {
                string warning = context.API.GetTranslation("flowlauncher_plugin_program_suffixes_cannot_empty");  // TODO text update
                MessageBox.Show(warning);
                return;
            }

            _settings.CustomSuffixes = suffixes;
            _settings.CustomProtocols = protocols;

            DialogResult = true;
        }

        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {
            apprefMS.IsChecked = true;
            exe.IsChecked = true;
            lnk.IsChecked = true;
            CustomFiles.IsChecked = false;

            steam.IsChecked = true;
            epic.IsChecked = true;
            http.IsChecked = false;
            CustomProtocol.IsChecked = false;
        }
    }
}
