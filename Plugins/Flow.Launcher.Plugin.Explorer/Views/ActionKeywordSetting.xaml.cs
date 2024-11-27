using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    /// <summary>
    /// Interaction logic for ActionKeywordSetting.xaml
    /// </summary>
    public partial class ActionKeywordSetting : INotifyPropertyChanged
    {
        private ActionKeywordModel CurrentActionKeyword { get; }

        public string ActionKeyword
        {
            get => actionKeyword;
            set
            {
                // Set Enable to be true if user change ActionKeyword
                KeywordEnabled = true;
                _ = SetField(ref actionKeyword, value);
            }
        }

        public bool KeywordEnabled
        {
            get => _keywordEnabled;
            set => SetField(ref _keywordEnabled, value);
        }

        private string actionKeyword;
        private readonly IPublicAPI api;
        private bool _keywordEnabled;

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

            if (ActionKeyword == Query.GlobalPluginWildcardSign)
                switch (CurrentActionKeyword.KeywordProperty, KeywordEnabled)
                {
                    case (Settings.ActionKeyword.FileContentSearchActionKeyword, true):
                        api.ShowMsgBox(api.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));
                        return;
                    case (Settings.ActionKeyword.QuickAccessActionKeyword, true):
                        api.ShowMsgBox(api.GetTranslation("plugin_explorer_quickaccess_globalActionKeywordInvalid"));
                        return;
                }

            if (!KeywordEnabled || !api.ActionKeywordAssigned(ActionKeyword))
            {
                DialogResult = true;
                Close();
                return;
            }

            // The keyword is not valid, so show message
            api.ShowMsgBox(api.GetTranslation("newActionKeywordsHasBeenAssigned"));
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
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
