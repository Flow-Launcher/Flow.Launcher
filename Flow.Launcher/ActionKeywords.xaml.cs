using System.Windows;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Core;
using System.Linq;
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

            var addedActionKeywords = newActionKeywords.Except(oldActionKeywords).ToList();
            var removedActionKeywords = oldActionKeywords.Except(newActionKeywords).ToList();
            if (!addedActionKeywords.Any(App.API.ActionKeywordAssigned))
            {
                if (oldActionKeywords.Count != newActionKeywords.Count)
                {
                    ReplaceActionKeyword(plugin.Metadata.ID, removedActionKeywords, addedActionKeywords);
                    return;
                }

                var sortedOldActionKeywords = oldActionKeywords.OrderBy(s => s).ToList();
                var sortedNewActionKeywords = newActionKeywords.OrderBy(s => s).ToList();

                if (sortedOldActionKeywords.SequenceEqual(sortedNewActionKeywords))
                {
                    // User just changes the sequence of action keywords
                    var msg = translater.GetTranslation("newActionKeywordsSameAsOld");
                    MessageBoxEx.Show(msg);
                }
                else
                {
                    ReplaceActionKeyword(plugin.Metadata.ID, removedActionKeywords, addedActionKeywords);
                }
            }
            else
            {
                string msg = translater.GetTranslation("newActionKeywordsHasBeenAssigned");
                App.API.ShowMsgBox(msg);
            }
        }

        private void ReplaceActionKeyword(string id, IReadOnlyList<string> removedActionKeywords, IReadOnlyList<string> addedActionKeywords)
        {
            foreach (var actionKeyword in removedActionKeywords)
            {
                App.API.RemoveActionKeyword(id, actionKeyword);
            }
            foreach (var actionKeyword in addedActionKeywords)
            {
                App.API.AddActionKeyword(id, actionKeyword);
            }

            // Update action keywords text and close window
            pluginViewModel.OnActionKeywordsChanged();
            Close();
        }
    }
}
