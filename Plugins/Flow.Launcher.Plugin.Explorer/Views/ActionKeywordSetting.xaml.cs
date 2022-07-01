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
        public ActionKeywordModel CurrentActionKeyword { get; set; }

        public string ActionKeyword
        {
            get => actionKeyword;
            set
            {
                // Set Enable to be true if user change ActionKeyword
                KeywordEnabled = true;
                actionKeyword = value;
            }
        }

        public bool KeywordEnabled { get; set; }

        private string actionKeyword;
        private readonly IPublicAPI api;

        public ActionKeywordSetting(ActionKeywordModel selectedActionKeyword, IPublicAPI api)
        {
            CurrentActionKeyword = selectedActionKeyword;
            this.api = api;
            ActionKeyword = selectedActionKeyword.Keyword;
            KeywordEnabled = selectedActionKeyword.Enabled;

            InitializeComponent();

            TxtCurrentActionKeyword.Focus();
        }

        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ActionKeyword))
                ActionKeyword = Query.GlobalPluginWildcardSign;

            if (CurrentActionKeyword.Keyword == ActionKeyword && CurrentActionKeyword.Enabled == KeywordEnabled)
            {
                DialogResult = false;
                Close();
                return;
            }

            if (ActionKeyword == "")
            {
                ActionKeyword = "*";
            }

            if (ActionKeyword == Query.GlobalPluginWildcardSign)
                switch (CurrentActionKeyword.KeywordProperty)
                {
                    case Settings.ActionKeyword.FileContentSearchActionKeyword:
                        MessageBox.Show(api.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));
                        return;
                    case Settings.ActionKeyword.QuickAccessActionKeyword:
                        MessageBox.Show(api.GetTranslation("plugin_explorer_quickaccess_globalActionKeywordInvalid"));
                        return;
                }

            if (!KeywordEnabled || !api.ActionKeywordAssigned(ActionKeyword))
            {
                DialogResult = true;
                Close();
                return;
            }

            // The keyword is not valid, so show message
            MessageBox.Show(api.GetTranslation("newActionKeywordsHasBeenAssigned"));
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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