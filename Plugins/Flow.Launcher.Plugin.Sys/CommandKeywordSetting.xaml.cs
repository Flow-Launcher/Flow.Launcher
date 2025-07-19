using System.Windows;

namespace Flow.Launcher.Plugin.Sys
{
    public partial class CommandKeywordSettingWindow
    {
        private readonly Command _oldSearchSource;
        private readonly PluginInitContext _context;

        public CommandKeywordSettingWindow(PluginInitContext context, Command old)
        {
            _context = context;
            _oldSearchSource = old;
            InitializeComponent();
            CommandKeyword.Text = old.Keyword;
            CommandKeywordTips.Text = string.Format(_context.API.GetTranslation("flowlauncher_plugin_sys_custom_command_keyword_tip"), old.Name);
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            var keyword = CommandKeyword.Text;
            if (string.IsNullOrEmpty(keyword))
            {
                var warning = _context.API.GetTranslation("flowlauncher_plugin_sys_input_command_keyword");
                _context.API.ShowMsgBox(warning);
            }
            else
            {
                _oldSearchSource.Keyword = keyword;
                Close();
            }
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            // Key is the default value of this command
            CommandKeyword.Text = _oldSearchSource.Key;
        }
    }
}
