using System.Windows;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using System.Linq;
using System.Collections.Generic;

namespace Flow.Launcher
{
    public partial class ActionKeywords
    {
        private readonly PluginPair _plugin;
        private readonly PluginViewModel _pluginViewModel;

        public ActionKeywords(PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            _plugin = pluginViewModel.PluginPair;
            _pluginViewModel = pluginViewModel;
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeparator, _plugin.Metadata.ActionKeywords);
            tbAction.Text = tbOldActionKeyword.Text;
            tbAction.SelectAll();
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs _)
        {
            var oldActionKeywords = _plugin.Metadata.ActionKeywords;

            var newActionKeywords = tbAction.Text.Split(Query.ActionKeywordSeparator)
                                                 .Where(s => !string.IsNullOrEmpty(s))
                                                 .Distinct()
                                                 .ToList();

            newActionKeywords = newActionKeywords.Count > 0 ? newActionKeywords : new() { Query.GlobalPluginWildcardSign };

            var addedActionKeywords = newActionKeywords.Except(oldActionKeywords).ToList();
            var removedActionKeywords = oldActionKeywords.Except(newActionKeywords).ToList();

            if (addedActionKeywords.Any(App.API.ActionKeywordAssigned))
            {
                App.API.ShowMsgBox(App.API.GetTranslation("newActionKeywordsHasBeenAssigned"));
                return;
            }

            if (oldActionKeywords.Count != newActionKeywords.Count)
            {
                ReplaceActionKeyword(_plugin.Metadata.ID, removedActionKeywords, addedActionKeywords);
                return;
            }

            var sortedOldActionKeywords = oldActionKeywords.OrderBy(s => s).ToList();
            var sortedNewActionKeywords = newActionKeywords.OrderBy(s => s).ToList();

            if (sortedOldActionKeywords.SequenceEqual(sortedNewActionKeywords))
            {
                // User just changes the sequence of action keywords
                App.API.ShowMsgBox(App.API.GetTranslation("newActionKeywordsSameAsOld"));
            }
            else
            {
                ReplaceActionKeyword(_plugin.Metadata.ID, removedActionKeywords, addedActionKeywords);
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
            _pluginViewModel.OnActionKeywordsTextChanged();
            Close();
        }
    }
}
