using Flow.Launcher.Plugin.Explorer.ViewModels;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    /// <summary>
    /// Interaction logic for ActionKeywordSetting.xaml
    /// </summary>
    public partial class ActionKeywordSetting : Window
    {
        private SettingsViewModel settingsViewModel;

        public ActionKeywordView CurrentActionKeyword { get; set; }

        public string ActionKeyword
        {
            get => actionKeyword;
            set
            {
                // Set Enable to be true if user change ActionKeyword
                Enabled = true;
                actionKeyword = value;
            }
        }

        public bool Enabled { get; set; }

        private string actionKeyword;

        public ActionKeywordSetting(SettingsViewModel settingsViewModel,
            ActionKeywordView selectedActionKeyword)
        {
            this.settingsViewModel = settingsViewModel;

            CurrentActionKeyword = selectedActionKeyword;

            ActionKeyword = selectedActionKeyword.Keyword;
            Enabled = selectedActionKeyword.Enabled;

            InitializeComponent();

            TxtCurrentActionKeyword.Focus();
        }

        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ActionKeyword))
                ActionKeyword = Query.GlobalPluginWildcardSign;

            if (CurrentActionKeyword.Keyword == ActionKeyword && CurrentActionKeyword.Enabled == Enabled)
            {
                Close();
                return;
            }


            if (ActionKeyword == Query.GlobalPluginWildcardSign)
                switch (CurrentActionKeyword.KeywordProperty)
                {
                    case Settings.ActionKeyword.FileContentSearchActionKeyword:
                        MessageBox.Show(settingsViewModel.Context.API.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));
                        return;
                    case Settings.ActionKeyword.QuickAccessActionKeyword:
                        MessageBox.Show(settingsViewModel.Context.API.GetTranslation("plugin_explorer_quickaccess_globalActionKeywordInvalid"));
                        return;
                }

            var oldActionKeyword = CurrentActionKeyword.Keyword;

            if (!Enabled || !settingsViewModel.IsActionKeywordAlreadyAssigned(ActionKeyword))
            {
                // Update View Data
                CurrentActionKeyword.Keyword = Enabled == true ? ActionKeyword : Query.GlobalPluginWildcardSign;
                CurrentActionKeyword.Enabled = Enabled;

                switch (Enabled)
                {
                    // reset to global so it does not take up an action keyword when disabled
                    // not for null Enable plugin
                    case false when oldActionKeyword != Query.GlobalPluginWildcardSign:
                        settingsViewModel.UpdateActionKeyword(CurrentActionKeyword.KeywordProperty,
                            Query.GlobalPluginWildcardSign, oldActionKeyword);
                        break;
                    default:
                        settingsViewModel.UpdateActionKeyword(CurrentActionKeyword.KeywordProperty,
                            CurrentActionKeyword.Keyword, oldActionKeyword);
                        break;
                }

                Close();
                return;
            }

            // The keyword is not valid, so show message
            MessageBox.Show(settingsViewModel.Context.API.GetTranslation("newActionKeywordsHasBeenAssigned"));
        }

        private void TxtCurrentActionKeyword_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DownButton.Focus();
                OnDoneButtonClick(sender, e);
                e.Handled = true;
            }
        }
    }
}