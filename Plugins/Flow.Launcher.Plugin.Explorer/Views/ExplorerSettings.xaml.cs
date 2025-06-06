using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    public partial class ExplorerSettings
    {
        private readonly SettingsViewModel viewModel;
        private List<Expander> _expanders;

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
            this.viewModel = viewModel;
            // DataContext = viewModel; // Removed duplicate

            ActionKeywordModel.Init(viewModel.Settings);

            // Ensure lbxAccessLinks and lbxExcludedPaths are initialized before accessing Items
            // This might require Loaded event if they are not immediately available
            // For now, assuming they are available after InitializeComponent()
            if (lbxAccessLinks != null)
                lbxAccessLinks.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));
            
            if (lbxExcludedPaths != null)
                lbxExcludedPaths.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));

            _expanders = new List<Expander>
            {
                GeneralSettingsExpander,
                ContextMenuExpander,
                PreviewPanelExpander,
                EverythingExpander,
                ActionKeywordsExpander,
                QuickAccessExpander,
                ExcludedPathsExpander
            };
        }

        private void AccessLinkDragDrop(string containerName, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files == null || !files.Any())
            {
                return;
            }
            foreach (var s in files)
            {
                if (Directory.Exists(s))
                {
                    var newFolderLink = new AccessLink
                    {
                        Path = s
                    };
                    viewModel.AppendLink(containerName, newFolderLink);
                }
            }
        }

        private void lbxAccessLinks_DragEnter(object sender, DragEventArgs e)
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

        private void btnOpenIndexingOptions_Click(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.OpenWindowsIndexingOptions();
        }
        private void EverythingSortOptionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tbFastSortWarning is not null)
            {
                tbFastSortWarning.Visibility = viewModel.FastSortWarningVisibility;
                tbFastSortWarning.Text = viewModel.SortOptionWarningMessage;
            }
        }
        private void LbxAccessLinks_OnDrop(object sender, DragEventArgs e)
        {
            AccessLinkDragDrop("QuickAccessLink", e);
        }
        private void LbxExcludedPaths_OnDrop(object sender, DragEventArgs e)
        {
            AccessLinkDragDrop("IndexSearchExcludedPath", e);
        }

        private void AllowOnlyNumericInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = e.Text.ToCharArray().Any(c => !char.IsDigit(c));
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander expandedExpander)
            {
                // Ensure _expanders is not null and contains items
                if (_expanders == null || !_expanders.Any()) return;

                foreach (var expander in _expanders)
                {
                    if (expander != null && expander != expandedExpander && expander.IsExpanded)
                    {
                        expander.IsExpanded = false;
                    }
                }
            }
        }
    }
}
