using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System;
using System.Collections;
using System.Collections.Generic;

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
            this.Loaded += SettingsControl_Loaded;
        }
        
        private void SettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            // After the ListView is loaded, sort by Tag in ascending order
            if (SearchSourcesListView.ItemsSource != null)
            {
                // Apply initial sorting by Tag column
                Sort("Tag", ListSortDirection.Ascending);
        
                // Display an arrow on the sorted column (optional)
                var tagColumn = GetColumnByHeader("Tag");
                if (tagColumn != null)
                {
                    tagColumn.HeaderTemplate = Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    _lastHeaderClicked = tagColumn.Header as GridViewColumnHeader;
                    _lastDirection = ListSortDirection.Ascending;
                }
            }
        }

        // Find column by header name
        private GridViewColumn GetColumnByHeader(string header)
        {
            if (SearchSourcesListView.View is GridView gridView)
            {
                foreach (var column in gridView.Columns)
                {
                    if (column.Header != null && column.Header.ToString() == header)
                        return column;
                }
            }
            return null;
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
                    _context.API.RemoveActionKeyword(id, selected.ActionKeyword);
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

            // Special handling for Tag sorting
            if (sortBy == "Tag")
            {
                // Apply custom sorting (using TagComparer)
                if (dataView is ListCollectionView listView)
                {
                    listView.CustomSort = new TagComparer(direction);
                }
            }
            else
            {
                // Normal sorting
                SortDescription sd = new SortDescription(sortBy, direction);
                dataView.SortDescriptions.Add(sd);
            }

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
        
        
        public class TagComparer : IComparer
        {
            private readonly ListSortDirection _direction;

            public TagComparer(ListSortDirection direction)
            {
                _direction = direction;
            }

            public int Compare(object x, object y)
            {
                if (x is SearchSource sourceX && y is SearchSource sourceY)
                {
                    string tagX = sourceX.Tag;
                    string tagY = sourceY.Tag;

                    bool isEmptyX = string.IsNullOrWhiteSpace(tagX);
                    bool isEmptyY = string.IsNullOrWhiteSpace(tagY);

                    // If both are empty tags, they are equal
                    if (isEmptyX && isEmptyY)
                        return 0;

                    // If only x is an empty tag, it always goes to the back
                    if (isEmptyX)
                        return 1;

                    // If only y is an empty tag, it always goes to the front
                    if (isEmptyY)
                        return -1;

                    // If both have tags, compare as normal strings
                    int result = string.Compare(tagX, tagY, StringComparison.OrdinalIgnoreCase);

                    // Reverse the result according to the sorting direction
                    return _direction == ListSortDirection.Ascending ? result : -result;
                }

                return 0;
            }
        }
    }
}
