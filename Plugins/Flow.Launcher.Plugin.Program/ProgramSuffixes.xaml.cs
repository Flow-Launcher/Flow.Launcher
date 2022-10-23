using System;
using System.Collections.Generic;
using System.Windows;

namespace Flow.Launcher.Plugin.Program
{
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;
        private Settings _settings;
        public Dictionary<string, bool> SuffixesStaus => _settings.BuiltinSuffixesStatus;

        public ProgramSuffixes(PluginInitContext context, Settings settings)
        {
            this.context = context;
            _settings = settings;
            InitializeComponent();
            tbSuffixes.Text = string.Join(Settings.SuffixSeperator, _settings.CustomSuffixes);
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            var suffixes = tbSuffixes.Text.Split(Settings.SuffixSeperator, StringSplitOptions.RemoveEmptyEntries);

            if (suffixes.Length == 0 && _settings.UseCustomSuffixes)
            {
                string warning = context.API.GetTranslation("flowlauncher_plugin_program_suffixes_cannot_empty");
                MessageBox.Show(warning);
                return;
            }

            _settings.CustomSuffixes = suffixes;

            //string msg = context.API.GetTranslation("flowlauncher_plugin_program_update_file_suffixes");
            //MessageBox.Show(msg);

            DialogResult = true;
        }

        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {
            apprefMS.IsChecked = true;
            exe.IsChecked = true;
            lnk.IsChecked = true;
            CustomFiles.IsChecked = false;

        }
    }
}
