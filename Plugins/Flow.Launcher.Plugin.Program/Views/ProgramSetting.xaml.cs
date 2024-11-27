using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.Program.Views.Commands;
using Flow.Launcher.Plugin.Program.Programs;
using System.ComponentModel;
using System.Windows.Data;
using Flow.Launcher.Plugin.Program.ViewModels;

namespace Flow.Launcher.Plugin.Program.Views
{
    /// <summary>
    /// Interaction logic for ProgramSetting.xaml
    /// </summary>
    public partial class ProgramSetting : UserControl
    {
        private PluginInitContext context;
        private Settings _settings;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection;

        // We do not save all program sources to settings, so using
        // this as temporary holder for displaying all loaded programs sources.
        internal static List<ProgramSource> ProgramSettingDisplayList { get; set; }

        public bool EnableDescription
        {
            get => _settings.EnableDescription;
            set
            {
                Main.ResetCache();
                _settings.EnableDescription = value;
            }
        }

        public bool HideAppsPath
        {
            get => _settings.HideAppsPath;
            set
            {
                Main.ResetCache();
                _settings.HideAppsPath = value;
            }
        }

        public bool HideUninstallers
        {
            get => _settings.HideUninstallers;
            set
            {
                Main.ResetCache();
                _settings.HideUninstallers = value;
            }
        }

        public bool EnableRegistrySource
        {
            get => _settings.EnableRegistrySource;
            set
            {
                _settings.EnableRegistrySource = value;
                ReIndexing();
            }
        }

        public bool EnableStartMenuSource
        {
            get => _settings.EnableStartMenuSource;
            set
            {
                _settings.EnableStartMenuSource = value;
                ReIndexing();
            }
        }

        public bool EnablePATHSource
        {
            get => _settings.EnablePathSource;
            set
            {
                _settings.EnablePathSource = value;
                ReIndexing();
            }
        }

        public bool EnableUWP
        {
            get => _settings.EnableUWP;
            set
            {
                _settings.EnableUWP = value;
                ReIndexing();
            }
        }

        public bool ShowUWPCheckbox => UWPPackage.SupportUWP();

        public ProgramSetting(PluginInitContext context, Settings settings, Win32[] win32s, UWPApp[] uwps)
        {
            this.context = context;
            _settings = settings;
            Loaded += Setting_Loaded;
            InitializeComponent();
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            ProgramSettingDisplayList = ProgramSettingDisplay.LoadProgramSources();
            programSourceView.ItemsSource = ProgramSettingDisplayList;

            ViewRefresh();
        }

        private void ViewRefresh()
        {
            if (programSourceView.Items.Count == 0
                && btnProgramSourceStatus.Visibility == Visibility.Visible
                && btnEditProgramSource.Visibility == Visibility.Visible)
            {
                btnProgramSourceStatus.Visibility = Visibility.Hidden;
                btnEditProgramSource.Visibility = Visibility.Hidden;
            }

            if (programSourceView.Items.Count > 0
                && btnProgramSourceStatus.Visibility == Visibility.Hidden
                && btnEditProgramSource.Visibility == Visibility.Hidden)
            {
                btnProgramSourceStatus.Visibility = Visibility.Visible;
                btnEditProgramSource.Visibility = Visibility.Visible;
            }

            programSourceView.Items.Refresh();
        }

        private async void ReIndexing()
        {
            ViewRefresh();
            indexingPanel.Visibility = Visibility.Visible;
            await Main.IndexProgramsAsync();
            indexingPanel.Visibility = Visibility.Hidden;
        }

        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = new AddProgramSourceViewModel(context, _settings);
            var add = new AddProgramSource(vm);
            if (add.ShowDialog() ?? false)
            {
                ReIndexing();
            }

            ViewRefresh();
        }

        private void DeleteProgramSources(List<ProgramSource> itemsToDelete)
        {
            itemsToDelete.ForEach(t1 => _settings.ProgramSources
                .Remove(_settings.ProgramSources
                    .Where(x => x.UniqueIdentifier == t1.UniqueIdentifier)
                    .FirstOrDefault()));
            itemsToDelete.ForEach(x => ProgramSettingDisplayList.Remove(x));

            ReIndexing();
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
            EditProgramSource(selectedProgramSource);
        }

        private void EditProgramSource(ProgramSource selectedProgramSource)
        {
            if (selectedProgramSource == null)
            {
                string msg = context.API.GetTranslation("flowlauncher_plugin_program_pls_select_program_source");
                context.API.ShowMsgBox(msg);
            }
            else
            {
                var vm = new AddProgramSourceViewModel(context, _settings, selectedProgramSource);
                var add = new AddProgramSource(vm);
                int selectedIndex = programSourceView.SelectedIndex;
                // https://stackoverflow.com/questions/16789360/wpf-listbox-items-with-changing-hashcode
                // Or it can't be unselected after changing Location
                programSourceView.UnselectAll();
                if (add.ShowDialog() ?? false)
                {
                    if (selectedProgramSource.Enabled)
                    {
                        ProgramSettingDisplay.SetProgramSourcesStatus(new List<ProgramSource> { selectedProgramSource },
                            true); // sync status in win32, uwp and disabled
                        ProgramSettingDisplay.RemoveDisabledFromSettings();
                    }
                    else
                    {
                        ProgramSettingDisplay.SetProgramSourcesStatus(new List<ProgramSource> { selectedProgramSource },
                            false);
                        ProgramSettingDisplay.StoreDisabledInSettings();
                    }

                    ReIndexing();
                }

                programSourceView.SelectedIndex = selectedIndex;
            }
        }

        private void btnReindex_Click(object sender, RoutedEventArgs e)
        {
            ReIndexing();
        }

        private void BtnProgramSuffixes_OnClick(object sender, RoutedEventArgs e)
        {
            var p = new ProgramSuffixes(context, _settings);
            if (p.ShowDialog() ?? false)
            {
                ReIndexing();
            }
        }

        private void programSourceView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void programSourceView_Drop(object sender, DragEventArgs e)
        {
            var directories = (string[])e.Data.GetData(DataFormats.FileDrop);

            var directoriesToAdd = new List<ProgramSource>();

            if (directories != null && directories.Length > 0)
            {
                foreach (string directory in directories)
                {
                    if (Directory.Exists(directory)
                        && !ProgramSettingDisplayList.Any(x =>
                            x.UniqueIdentifier.Equals(directory, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        var source = new ProgramSource(directory);

                        directoriesToAdd.Add(source);
                    }
                }

                if (directoriesToAdd.Count > 0)
                {
                    directoriesToAdd.ForEach(x => _settings.ProgramSources.Add(x));
                    directoriesToAdd.ForEach(x => ProgramSettingDisplayList.Add(x));

                    ViewRefresh();
                    ReIndexing();
                }
            }
        }

        private void btnLoadAllProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSettingDisplay.DisplayAllPrograms();

            ViewRefresh();
        }

        private void btnProgramSourceStatus_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = programSourceView
                .SelectedItems.Cast<ProgramSource>()
                .ToList();

            if (selectedItems.Count == 0)
            {
                string msg = context.API.GetTranslation("flowlauncher_plugin_program_pls_select_program_source");
                context.API.ShowMsgBox(msg);
                return;
            }

            if (IsAllItemsUserAdded(selectedItems))
            {
                var msg = string.Format(
                    context.API.GetTranslation("flowlauncher_plugin_program_delete_program_source"));

                if (context.API.ShowMsgBox(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    return;
                }

                DeleteProgramSources(selectedItems);
            }
            else if (HasMoreOrEqualEnabledItems(selectedItems))
            {
                ProgramSettingDisplay.SetProgramSourcesStatus(selectedItems, false);

                ProgramSettingDisplay.StoreDisabledInSettings();
            }
            else
            {
                ProgramSettingDisplay.SetProgramSourcesStatus(selectedItems, true);

                ProgramSettingDisplay.RemoveDisabledFromSettings();
            }

            if (selectedItems.IsReindexRequired())
                ReIndexing();

            programSourceView.SelectedItems.Clear();

            ViewRefresh();
        }

        private void ProgramSourceView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            programSourceView.SelectedItems.Clear();
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
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

                    Sort(sortBy, direction);

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(programSourceView.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private static bool HasMoreOrEqualEnabledItems(List<ProgramSource> items)
        {
            var enableCount = items.Where(x => x.Enabled).Count();
            return enableCount >= items.Count - enableCount;
        }

        private void programSourceView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItems = programSourceView
                .SelectedItems.Cast<ProgramSource>()
                .ToList();

            if (IsAllItemsUserAdded(selectedItems))
            {
                btnProgramSourceStatus.Content = context.API.GetTranslation("flowlauncher_plugin_program_delete");
            }
            else if (HasMoreOrEqualEnabledItems(selectedItems))
            {
                btnProgramSourceStatus.Content = context.API.GetTranslation("flowlauncher_plugin_program_disable");
            }
            else
            {
                btnProgramSourceStatus.Content = context.API.GetTranslation("flowlauncher_plugin_program_enable");
            }
        }

        private void programSourceView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is ProgramSource)
            {
                var selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
                EditProgramSource(selectedProgramSource);
            }
        }

        private bool IsAllItemsUserAdded(List<ProgramSource> items)
        {
            return items.All(x => _settings.ProgramSources.Any(y => y.UniqueIdentifier == x.UniqueIdentifier));
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            GridView gView = listView.View as GridView;

            var workingWidth =
                listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar
            var col1 = 0.25;
            var col2 = 0.15;
            var col3 = 0.60;

            gView.Columns[0].Width = workingWidth * col1;
            gView.Columns[1].Width = workingWidth * col2;
            gView.Columns[2].Width = workingWidth * col3;
        }
    }
}
