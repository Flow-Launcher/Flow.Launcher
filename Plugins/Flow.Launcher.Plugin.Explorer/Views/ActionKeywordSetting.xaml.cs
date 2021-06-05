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

        private string oldActionKeyword;

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

            oldActionKeyword = selectedActionKeyword.Keyword;

            this.actionKeywordListView = actionKeywordListView;
            
            InitializeComponent();

        }

        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentActionKeyword.Keyword))
                return;

            if (settingsViewModel.IsNewActionKeywordGlobal(CurrentActionKeyword.Keyword)
                && CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.FileContentSearchActionKeyword)
            {
                MessageBox.Show(
                    settingsViewModel.Context.API.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));

                return;
            }

            if (!settingsViewModel.IsActionKeywordAlreadyAssigned(CurrentActionKeyword.Keyword, oldActionKeyword))
            {
                settingsViewModel.UpdateActionKeyword(CurrentActionKeyword.KeywordProperty, CurrentActionKeyword.Keyword, oldActionKeyword);

                actionKeywordListView.FirstOrDefault(x => x.Description == CurrentActionKeyword.Description).Keyword =
                    CurrentActionKeyword.Keyword;

                // automatically help users set this to enabled if an action keyword is set and currently disabled
                if (CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.IndexSearchActionKeyword
                    && !settings.EnabledIndexOnlySearchKeyword)
                    settings.EnabledIndexOnlySearchKeyword = true;

                if (CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.PathSearchActionKeyword
                    && !settings.EnabledPathSearchKeyword)
                    settings.EnabledPathSearchKeyword = true;

                if (CurrentActionKeyword.KeywordProperty == Settings.ActionKeyword.SearchActionKeyword
                    && !settings.EnableSearchActionKeyword)
                    settings.EnableSearchActionKeyword = true;

                Close();

                return;
            }

            // reset to global so it does not take up an action keyword when disabled
            if (CurrentActionKeyword.Enabled is not null && CurrentActionKeyword.Enabled == false && CurrentActionKeyword.Keyword != Query.GlobalPluginWildcardSign)
                settingsViewModel.UpdateActionKeyword(CurrentActionKeyword.KeywordProperty, Query.GlobalPluginWildcardSign, oldActionKeyword);

            Close();

            return;
        }
    }
}