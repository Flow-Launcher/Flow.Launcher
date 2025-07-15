using System;
using System.Collections.Generic;
using System.Windows;

namespace Flow.Launcher.Plugin.Program
{
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;
        private Settings _settings;
        public Dictionary<string, bool> SuffixesStatus { get; set; }
        public Dictionary<string, bool> ProtocolsStatus { get; set; }
        public bool UseCustomSuffixes { get; set; }
        public bool UseCustomProtocols { get; set; }

        public ProgramSuffixes(PluginInitContext context, Settings settings)
        {
            this.context = context;
            _settings = settings;
            SuffixesStatus = new Dictionary<string, bool>(_settings.BuiltinSuffixesStatus);
            ProtocolsStatus = new Dictionary<string, bool>(_settings.BuiltinProtocolsStatus);
            UseCustomSuffixes = _settings.UseCustomSuffixes;
            UseCustomProtocols = _settings.UseCustomProtocols;
            InitializeComponent();
            tbSuffixes.Text = string.Join(Settings.SuffixSeparator, _settings.CustomSuffixes);
            tbProtocols.Text = string.Join(Settings.SuffixSeparator, _settings.CustomProtocols);
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            var suffixes = tbSuffixes.Text.Split(Settings.SuffixSeparator, StringSplitOptions.RemoveEmptyEntries);
            var protocols = tbProtocols.Text.Split(Settings.SuffixSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (suffixes.Length == 0 && UseCustomSuffixes)
            {
                string warning = context.API.GetTranslation("flowlauncher_plugin_program_suffixes_cannot_empty");
                context.API.ShowMsgBox(warning);
                return;
            }

            if (protocols.Length == 0 && UseCustomProtocols)
            {
                string warning = context.API.GetTranslation("flowlauncher_plugin_protocols_cannot_empty");
                context.API.ShowMsgBox(warning);
                return;
            }

            _settings.CustomSuffixes = suffixes;
            _settings.CustomProtocols = protocols;
            _settings.BuiltinSuffixesStatus = new Dictionary<string, bool>(SuffixesStatus);
            _settings.BuiltinProtocolsStatus = new Dictionary<string, bool>(ProtocolsStatus);
            _settings.UseCustomSuffixes = UseCustomSuffixes;
            _settings.UseCustomProtocols = UseCustomProtocols;

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
