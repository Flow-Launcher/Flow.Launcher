using System.Windows;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ActionKeywords
    {
        private readonly PluginPair plugin;
        private readonly PluginViewModel pluginViewModel;

        public ActionKeywords(PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            plugin = pluginViewModel.PluginPair;
            this.pluginViewModel = pluginViewModel;
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeparator, plugin.Metadata.ActionKeywords.ToArray());
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs _)
        {
            var oldActionKeyword = plugin.Metadata.ActionKeywords[0];
            var newActionKeyword = tbAction.Text.Trim();
            newActionKeyword = newActionKeyword.Length > 0 ? newActionKeyword : "*";
            
            if (!PluginViewModel.IsActionKeywordRegistered(newActionKeyword))
            {
                pluginViewModel.ChangeActionKeyword(newActionKeyword, oldActionKeyword);
                Close();
            }
            else
            {
                string msg = App.API.GetTranslation("newActionKeywordsHasBeenAssigned");
                App.API.ShowMsgBox(msg);
            }
        }
    }
}
