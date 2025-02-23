using System.Windows;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Core;
using System.Linq;
using Flow.Launcher.Core.Plugin;
using System.Collections.Generic;

namespace Flow.Launcher
{
    public partial class ActionKeywords
    {
        private readonly PluginPair plugin;
        private readonly Internationalization translater = InternationalizationManager.Instance;
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
            var oldActionKeywords = plugin.Metadata.ActionKeywords;

            var newActionKeywords = tbAction.Text.Split(Query.ActionKeywordSeparator).ToList();
            newActionKeywords.RemoveAll(string.IsNullOrEmpty);
            newActionKeywords = newActionKeywords.Distinct().ToList();

            newActionKeywords = newActionKeywords.Count > 0 ? newActionKeywords : new() { Query.GlobalPluginWildcardSign };

            if (!newActionKeywords.Except(oldActionKeywords).Any(PluginManager.ActionKeywordRegistered))
            {
                pluginViewModel.ChangeActionKeyword(newActionKeywords, oldActionKeywords);
                Close();
            }
            else
            {
                string msg = translater.GetTranslation("newActionKeywordsHasBeenAssigned");
                MessageBoxEx.Show(msg);
            }
        }
    }
}
