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
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            var newActionKeyword = txtCurrentActionKeyword.Text;

            if (string.IsNullOrEmpty(newActionKeyword))
                return;

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
    }
}
