using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Plugin.Explorer.ViewModels;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    [INotifyPropertyChanged]
    public partial class ActionKeywordSetting
    {
        private ActionKeywordModel CurrentActionKeyword { get; }

        public string ActionKeyword
        {
            get => actionKeyword;
            set
            {
                // Set Enable to be true if user change ActionKeyword
                KeywordEnabled = true;
                _ = SetProperty(ref actionKeyword, value);
            }
        }

        public bool KeywordEnabled
        {
            get => _keywordEnabled;
            set => _ = SetProperty(ref _keywordEnabled, value);
        }

        private string actionKeyword;
        private readonly IPublicAPI _api;
        private bool _keywordEnabled;

        public ActionKeywordSetting(ActionKeywordModel selectedActionKeyword, IPublicAPI api)
        {
            CurrentActionKeyword = selectedActionKeyword;
            _api = api;
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

            if (ActionKeyword == Query.GlobalPluginWildcardSign)
                switch (CurrentActionKeyword.KeywordProperty, KeywordEnabled)
                {
                    case (Settings.ActionKeyword.FileContentSearchActionKeyword, true):
                        _api.ShowMsgBox(_api.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));
                        return;
                    case (Settings.ActionKeyword.QuickAccessActionKeyword, true):
                        _api.ShowMsgBox(_api.GetTranslation("plugin_explorer_quickaccess_globalActionKeywordInvalid"));
                        return;
                }

            if (!KeywordEnabled || !_api.ActionKeywordAssigned(ActionKeyword))
            {
                DialogResult = true;
                Close();
                return;
            }

            // The keyword is not valid, so show message
            _api.ShowMsgBox(_api.GetTranslation("newActionKeywordsHasBeenAssigned"));
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
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }
        
        
        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = e.DataObject.GetData(DataFormats.Text) as string;
                if (!string.IsNullOrEmpty(text) && text.Any(char.IsWhiteSpace))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}
