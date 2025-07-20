using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.WebSearch
{
    public partial class SearchSourceSettingWindow
    {
        private readonly SearchSource _oldSearchSource;
        private SearchSource _searchSource;
        private IList<SearchSource> _searchSources;
        private Action _action;
        private PluginInitContext _context;
        private IPublicAPI _api;
        private SearchSourceViewModel _viewModel;
        private string selectedNewIconImageFullPath;

        public SearchSourceSettingWindow(IList<SearchSource> sources, PluginInitContext context, SearchSource old)
        {
            _oldSearchSource = old;
            _viewModel = new SearchSourceViewModel {SearchSource = old.DeepCopy()};
            Initialize(sources, context, Action.Edit);
        }

        public SearchSourceSettingWindow(IList<SearchSource> sources, PluginInitContext context)
        {
            _viewModel = new SearchSourceViewModel {SearchSource = new SearchSource()};
            Initialize(sources, context, Action.Add);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void Initialize(IList<SearchSource> sources, PluginInitContext context, Action action)
        {
            InitializeComponent();
            DataContext = _viewModel;
            _searchSource = _viewModel.SearchSource;
            _searchSources = sources;
            _action = action;
            _context = context;
            _api = _context.API;

            _viewModel.SetupCustomImagesDirectory();

            imgPreviewIcon.Source = await _viewModel.LoadPreviewIconAsync(_searchSource.IconPath);
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_searchSource.Title))
            {
                var warning = _api.GetTranslation("flowlauncher_plugin_websearch_input_title");
                _context.API.ShowMsgBox(warning);
            }
            else if (string.IsNullOrEmpty(_searchSource.Url))
            {
                var warning = _api.GetTranslation("flowlauncher_plugin_websearch_input_url");
                _context.API.ShowMsgBox(warning);
            }
            else if (string.IsNullOrEmpty(_searchSource.ActionKeyword))
            {
                var warning = _api.GetTranslation("flowlauncher_plugin_websearch_input_action_keyword");
                _context.API.ShowMsgBox(warning);
            }
            else if (_action == Action.Add)
            {
                AddSearchSource();
            }
            else if (_action == Action.Edit)
            {
                EditSearchSource();
            }
        }

        private void AddSearchSource()
        {
            var keyword = _searchSource.ActionKeyword;
            if (!_context.API.ActionKeywordAssigned(keyword))
            {
                var id = _context.CurrentPluginMetadata.ID;
                _context.API.AddActionKeyword(id, keyword);

                _searchSources.Add(_searchSource);

                Close();
            }
            else
            {
                var warning = _api.GetTranslation("newActionKeywordsHasBeenAssigned");
                _context.API.ShowMsgBox(warning);
            }
        }

        private void EditSearchSource()
        {
            var newKeyword = _searchSource.ActionKeyword;
            var oldKeyword = _oldSearchSource.ActionKeyword;
            if (!_context.API.ActionKeywordAssigned(newKeyword) || oldKeyword == newKeyword)
            {
                var id = _context.CurrentPluginMetadata.ID;
                _context.API.RemoveActionKeyword(id, oldKeyword);
                _context.API.AddActionKeyword(id, newKeyword);

                var index = _searchSources.IndexOf(_oldSearchSource);
                _searchSources[index] = _searchSource;

                Close();
            }
            else
            {
                var warning = _api.GetTranslation("newActionKeywordsHasBeenAssigned");
                _context.API.ShowMsgBox(warning);
            }

            if (!string.IsNullOrEmpty(selectedNewIconImageFullPath))
            {
                _viewModel.UpdateIconAttributes(_searchSource, selectedNewIconImageFullPath);

                _viewModel.CopyNewImageToUserDataDirectoryIfRequired(
                                   _searchSource, selectedNewIconImageFullPath, _oldSearchSource.IconPath);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void OnSelectIconClick(object sender, RoutedEventArgs e)
        {
            const string filter = "Image files (*.jpg, *.jpeg, *.gif, *.png, *.bmp) |*.jpg; *.jpeg; *.gif; *.png; *.bmp";
            var dialog = new OpenFileDialog {InitialDirectory = Main.CustomImagesDirectory, Filter = filter};

            var result = dialog.ShowDialog();
            if (result == true)
            {
                selectedNewIconImageFullPath = dialog.FileName;

                if (!string.IsNullOrEmpty(selectedNewIconImageFullPath))
                {
                    if (_viewModel.ShouldProvideHint(selectedNewIconImageFullPath))
                        _context.API.ShowMsgBox(_api.GetTranslation("flowlauncher_plugin_websearch_iconpath_hint"));
                    
                    imgPreviewIcon.Source = await _viewModel.LoadPreviewIconAsync(selectedNewIconImageFullPath);
                }
            }
        }

        //Block Space Input
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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

    public enum Action
    {
        Add,
        Edit
    }
}
