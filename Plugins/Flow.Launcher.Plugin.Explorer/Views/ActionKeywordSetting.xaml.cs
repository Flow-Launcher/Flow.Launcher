using Flow.Launcher.Plugin.Explorer.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    /// <summary>
    /// Interaction logic for ActionKeywordSetting.xaml
    /// </summary>
    public partial class ActionKeywordSetting : Window
    {
        private SettingsViewModel settingsViewModel;

        private ActionKeywordView currentActionKeyword;

        private List<ActionKeywordView> actionKeywordListView;

        private Settings settings;

        public ActionKeywordSetting(SettingsViewModel settingsViewModel, 
            List<ActionKeywordView> actionKeywordListView, 
            ActionKeywordView selectedActionKeyword, Settings settings)
        {
            InitializeComponent();

            this.settingsViewModel = settingsViewModel;

            this.settings = settings;

            currentActionKeyword = selectedActionKeyword;

            txtCurrentActionKeyword.Text = selectedActionKeyword.Keyword;

            this.actionKeywordListView = actionKeywordListView;

            // Search and File Content action keyword are not allowed to be disabled, they are the default search keywords.
            if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.SearchActionKeyword
                || currentActionKeyword.KeywordProperty == ActionKeywordProperty.FileContentSearchActionKeyword)
                chkActionKeywordEnabled.Visibility = Visibility.Collapsed;

            if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.IndexOnlySearchActionKeyword)
                chkActionKeywordEnabled.IsChecked = this.settings.EnabledIndexOnlySearchKeyword;

            if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.PathSearchActionKeyword)
                chkActionKeywordEnabled.IsChecked = this.settings.EnabledPathSearchKeyword;
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            var newActionKeyword = txtCurrentActionKeyword.Text;

            if (string.IsNullOrEmpty(newActionKeyword))
                return;

            if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.IndexOnlySearchActionKeyword)
            {
                // reset to global so it does not take up an action keyword when disabled
                if (!currentActionKeyword.Enabled && newActionKeyword != Query.GlobalPluginWildcardSign)
                    settingsViewModel.UpdateActionKeyword(currentActionKeyword.KeywordProperty, Query.GlobalPluginWildcardSign, currentActionKeyword.Keyword);

                settings.EnabledIndexOnlySearchKeyword = currentActionKeyword.Enabled;
            }

            if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.PathSearchActionKeyword)
            {
                // reset to global so it does not take up an action keyword when disabled
                if (!currentActionKeyword.Enabled && newActionKeyword != Query.GlobalPluginWildcardSign)
                    settingsViewModel.UpdateActionKeyword(currentActionKeyword.KeywordProperty, Query.GlobalPluginWildcardSign, currentActionKeyword.Keyword);

                settings.EnabledPathSearchKeyword = currentActionKeyword.Enabled;
            }

            if (newActionKeyword == currentActionKeyword.Keyword)
            {
                Close();

                return;
            }

            if (settingsViewModel.IsNewActionKeywordGlobal(newActionKeyword)
                && currentActionKeyword.KeywordProperty == ActionKeywordProperty.FileContentSearchActionKeyword)
            {
                MessageBox.Show(settingsViewModel.Context.API.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));

                return;
            }

            if (!settingsViewModel.IsActionKeywordAlreadyAssigned(newActionKeyword))
            {
                settingsViewModel.UpdateActionKeyword(currentActionKeyword.KeywordProperty, newActionKeyword, currentActionKeyword.Keyword);

                actionKeywordListView.FirstOrDefault(x => x.Description == currentActionKeyword.Description).Keyword = newActionKeyword;

                // automatically help users set this to enabled if an action keyword is set and currently disabled
                if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.IndexOnlySearchActionKeyword 
                    && !settings.EnabledIndexOnlySearchKeyword)
                    settings.EnabledIndexOnlySearchKeyword = true;

                if (currentActionKeyword.KeywordProperty == ActionKeywordProperty.PathSearchActionKeyword
                    && !settings.EnabledPathSearchKeyword)
                    settings.EnabledPathSearchKeyword = true;

                Close();

                return;
            }

            MessageBox.Show(settingsViewModel.Context.API.GetTranslation("newActionKeywordsHasBeenAssigned"));
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            Close();

            return;
        }
        private void OnActionKeywordEnabledChecked(object sender, RoutedEventArgs e)
        {
            currentActionKeyword.Enabled = true;
        }

        private void OnActionKeywordEnabledUnChecked(object sender, RoutedEventArgs e)
        {
            currentActionKeyword.Enabled = false;
        }
    }
}
