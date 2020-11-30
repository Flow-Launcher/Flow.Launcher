using Flow.Launcher.Plugin.Explorer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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

        public ActionKeywordSetting(SettingsViewModel settingsViewModel, List<ActionKeywordView> actionKeywordListView, ActionKeywordView selectedActionKeyword)
        {
            InitializeComponent();

            this.settingsViewModel = settingsViewModel;

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
                && currentActionKeyword.Description
                    == settingsViewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_filecontentsearch"))
            {
                MessageBox.Show(settingsViewModel.Context.API.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));

                return;
            }


            settingsViewModel.UpdateActionKeyword(newActionKeyword, currentActionKeyword.Keyword);

            actionKeywordListView.Where(x => x.Description == currentActionKeyword.Description).FirstOrDefault().Keyword = newActionKeyword;

            Close();

            return;
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            Close();

            return;
        }
    }
}
