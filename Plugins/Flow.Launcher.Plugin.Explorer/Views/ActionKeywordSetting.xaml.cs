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

        public ActionKeywordView CurrentActionKeyword { get; set; }

        private List<ActionKeywordView> actionKeywordListView;

        private Settings settings;

        public Visibility Visible => CurrentActionKeyword.Enabled is not null ? Visibility.Visible : Visibility.Collapsed;

        public ActionKeywordSetting(SettingsViewModel settingsViewModel,
            List<ActionKeywordView> actionKeywordListView,
            ActionKeywordView selectedActionKeyword, Settings settings)
        {
            this.settingsViewModel = settingsViewModel;

            this.settings = settings;

            CurrentActionKeyword = selectedActionKeyword;

            this.actionKeywordListView = actionKeywordListView;
            
            InitializeComponent();

        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            var newActionKeyword = TxtCurrentActionKeyword.Text;

            if (string.IsNullOrEmpty(newActionKeyword))
                return;

            // reset to global so it does not take up an action keyword when disabled
            if (!CurrentActionKeyword.Enabled is not null && newActionKeyword != Query.GlobalPluginWildcardSign)
                settingsViewModel.UpdateActionKeyword(CurrentActionKeyword.KeywordProperty,
                    Query.GlobalPluginWildcardSign, CurrentActionKeyword.Keyword);

            if (newActionKeyword == CurrentActionKeyword.Keyword)
            {
                Close();

                return;
            }

            if (settingsViewModel.IsNewActionKeywordGlobal(newActionKeyword)
                && CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.FileContentSearchActionKeyword)
            {
                MessageBox.Show(
                    settingsViewModel.Context.API.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));

                return;
            }

            if (!settingsViewModel.IsActionKeywordAlreadyAssigned(newActionKeyword))
            {
                settingsViewModel.UpdateActionKeyword(CurrentActionKeyword.KeywordProperty, newActionKeyword,
                    CurrentActionKeyword.Keyword);

                actionKeywordListView.FirstOrDefault(x => x.Description == CurrentActionKeyword.Description).Keyword =
                    newActionKeyword;

                // automatically help users set this to enabled if an action keyword is set and currently disabled
                if (CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.IndexSearchActionKeyword
                    && !settings.EnabledIndexOnlySearchKeyword)
                    settings.EnabledIndexOnlySearchKeyword = true;

                if (CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.PathSearchActionKeyword
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
    }
}