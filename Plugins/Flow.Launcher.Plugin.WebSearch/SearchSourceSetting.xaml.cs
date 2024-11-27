using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using Flow.Launcher.Core.Plugin;

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
            if (!PluginManager.ActionKeywordRegistered(keyword))
            {
                var id = _context.CurrentPluginMetadata.ID;
                PluginManager.AddActionKeyword(id, keyword);

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
            if (!PluginManager.ActionKeywordRegistered(newKeyword) || oldKeyword == newKeyword)
            {
                var id = _context.CurrentPluginMetadata.ID;
                PluginManager.ReplaceActionKeyword(id, oldKeyword, newKeyword);

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
    }

    public enum Action
    {
        Add,
        Edit
    }
}
