using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;


namespace Flow.Launcher.Plugin.Explorer.Views
{
    [INotifyPropertyChanged]
    public partial class RenameFile : Window
    {
        

        public string NewFileName
        {
            get => newFileName;
            set
            {
                _ = SetProperty(ref newFileName, value);
            }
        }


        private string newFileName = "gayTest";
        private string renamingText = "fes";
        public string RenamingText
        {
            get => renamingText;
            set
            {
                _ = SetProperty(ref renamingText, value);
            }
        }
        private readonly IPublicAPI _api;


        public RenameFile(IPublicAPI api)
        {
            _api = api;


            InitializeComponent();

            RenameTb.Focus();
            Deactivated += (s, e) =>
            {
                DialogResult = false;
                Close();

            };
            ShowInTaskbar = false;
            RenameTb.Select(RenameTb.Text.Length, 0);
        }

        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            

      

            // if (ActionKeyword == Query.GlobalPluginWildcardSign)
            //     switch (CurrentActionKeyword.KeywordProperty, KeywordEnabled)
            //     {
            //         case (Settings.ActionKeyword.FileContentSearchActionKeyword, true):
            //             _api.ShowMsgBox(_api.GetTranslation("plugin_explorer_globalActionKeywordInvalid"));
            //             return;
            //         case (Settings.ActionKeyword.QuickAccessActionKeyword, true):
            //             _api.ShowMsgBox(_api.GetTranslation("plugin_explorer_quickaccess_globalActionKeywordInvalid"));
            //             return;
            //     }

            // if (!KeywordEnabled || !_api.ActionKeywordAssigned(ActionKeyword))
            // {
            //     DialogResult = true;
            //     Close();
            //     return;
            // }

            // The keyword is not valid, so show message
            _api.ShowMsgBox("new action keyword");
        }

        private void BtnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void TxtCurrentActionKeyword_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnDone.Focus();
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
