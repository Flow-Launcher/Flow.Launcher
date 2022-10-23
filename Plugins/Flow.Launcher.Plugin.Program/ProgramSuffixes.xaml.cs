using System;
using System.Windows;

namespace Flow.Launcher.Plugin.Program
{
    public partial class ProgramSuffixes
    {
        private PluginInitContext context;
        private Settings _settings;

        public ProgramSuffixes(PluginInitContext context, Settings settings)
        {
            this.context = context;
            InitializeComponent();
            _settings = settings;
            tbSuffixes.Text = string.Join(Settings.SuffixSeperator.ToString(), _settings.CustomSuffixes);
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            var suffixes = tbSuffixes.Text.Split(Settings.SuffixSeperator, StringSplitOptions.RemoveEmptyEntries);

            if (suffixes.Length == 0)
            {
                string warning = context.API.GetTranslation("flowlauncher_plugin_program_suffixes_cannot_empty");
                MessageBox.Show(warning);
                return;
            }

            _settings.CustomSuffixes = suffixes;

            string msg = context.API.GetTranslation("flowlauncher_plugin_program_update_file_suffixes");
            MessageBox.Show(msg);

            DialogResult = true;
        }

        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
