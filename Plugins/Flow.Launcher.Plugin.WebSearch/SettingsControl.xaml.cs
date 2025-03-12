using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Core.Plugin;
using System.ComponentModel;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.WebSearch
{
    /// <summary>
    /// Interaction logic for WebSearchesSetting.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;

        public SettingsControl(PluginInitContext context, SettingsViewModel viewModel)
        {
            InitializeComponent();
            _context = context;
            _settings = viewModel.Settings;
            DataContext = viewModel;
        }

        private void OnAddSearchSearchClick(object sender, RoutedEventArgs e)
        {
            var setting = new SearchSourceSettingWindow(_settings.SearchSources, _context);
            setting.ShowDialog();
        }

        private void OnDeleteSearchSearchClick(object sender, RoutedEventArgs e)
        {
            if (_settings.SelectedSearchSource != null)
            {
                var selected = _settings.SelectedSearchSource;
                var warning = _context.API.GetTranslation("flowlauncher_plugin_websearch_delete_warning");
                var formated = string.Format(warning, selected.Title);

                var result = _context.API.ShowMsgBox(formated, string.Empty, MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    var id = _context.CurrentPluginMetadata.ID;
                    PluginManager.RemoveActionKeyword(id, selected.ActionKeyword);
                    _settings.SearchSources.Remove(selected);
                }
            }
        }

        private void OnEditSearchSourceClick(object sender, RoutedEventArgs e)
        {
            if (_settings.SelectedSearchSource != null)
            {
                var webSearch = new SearchSourceSettingWindow
                (
                    _settings.SearchSources, _context, _settings.SelectedSearchSource
                );

                webSearch.ShowDialog();
            }
        }

        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private void SortByColumn(object sender, RoutedEventArgs e)
        {
            ListSortDirection direction;

            if (e.OriginalSource is not GridViewColumnHeader headerClicked)
            {
                return;
            }

            if (headerClicked.Role == GridViewColumnHeaderRole.Padding)
            {
                return;
            }

            if (headerClicked != _lastHeaderClicked)
            {
                direction = ListSortDirection.Ascending;
            }
            else
            {
                if (_lastDirection == ListSortDirection.Ascending)
                {
                    direction = ListSortDirection.Descending;
                }
                else
                {
                    direction = ListSortDirection.Ascending;
                }
            }

            var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
            var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

            if(sortBy != null) { 
                Sort(sortBy, direction);

                if (direction == ListSortDirection.Ascending)
                {
                    headerClicked.Column.HeaderTemplate =
                      Resources["HeaderTemplateArrowUp"] as DataTemplate;
                }
                else
                {
                    headerClicked.Column.HeaderTemplate =
                      Resources["HeaderTemplateArrowDown"] as DataTemplate;
                }

                // Remove arrow from previously sorted header
                if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                {
                    _lastHeaderClicked.Column.HeaderTemplate = null;
                }

                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }
        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(SearchSourcesListView.ItemsSource);
            dataView.SortDescriptions.Clear();
            SortDescription sd = new(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void MouseDoubleClickItem(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is SearchSource && _settings.SelectedSearchSource != null)
            {
                var webSearch = new SearchSourceSettingWindow
                (
                    _settings.SearchSources, _context, _settings.SelectedSearchSource
                );

                webSearch.ShowDialog();
            }
        }
    }
}
