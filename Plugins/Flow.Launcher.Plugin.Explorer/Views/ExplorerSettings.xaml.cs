using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLink;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    /// <summary>
    /// Interaction logic for ExplorerSettings.xaml
    /// </summary>
    public partial class ExplorerSettings
    {
        private readonly SettingsViewModel viewModel;

        private List<ActionKeywordView> actionKeywordsListView;

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;

            lbxFolderLinks.ItemsSource = this.viewModel.Settings.QuickFolderAccessLinks;

            lbxExcludedPaths.ItemsSource = this.viewModel.Settings.IndexSearchExcludedSubdirectoryPaths;

            actionKeywordsListView = new List<ActionKeywordView>
            {
                new ActionKeywordView() 
                        { 
                            Description = viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_search"), 
                            Keyword = this.viewModel.Settings.SearchActionKeyword 
                        },
                new ActionKeywordView() 
                        { 
                            Description = viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_filecontentsearch"), 
                            Keyword = this.viewModel.Settings.FileContentSearchActionKeyword 
                        }
            };

            lbxActionKeywords.ItemsSource = actionKeywordsListView;

            RefreshView();
        }

        public void RefreshView()
        {
            lbxFolderLinks.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));

            lbxExcludedPaths.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));

            btnDelete.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Hidden;
            btnAdd.Visibility = Visibility.Hidden;

            if (expFolderLinks.IsExpanded || expExcludedPaths.IsExpanded || expActionKeywords.IsExpanded)
            {
                if (!expActionKeywords.IsExpanded)
                    btnAdd.Visibility = Visibility.Visible;

                if (expActionKeywords.IsExpanded
                    && btnEdit.Visibility == Visibility.Hidden)
                    btnEdit.Visibility = Visibility.Visible;

                if ((lbxFolderLinks.Items.Count == 0 && lbxExcludedPaths.Items.Count == 0)
                    && btnDelete.Visibility == Visibility.Visible
                    && btnEdit.Visibility == Visibility.Visible)
                {
                    btnDelete.Visibility = Visibility.Hidden;
                    btnEdit.Visibility = Visibility.Hidden;
                }

                if (expFolderLinks.IsExpanded
                    && lbxFolderLinks.Items.Count > 0
                    && btnDelete.Visibility == Visibility.Hidden
                    && btnEdit.Visibility == Visibility.Hidden)
                {
                    btnDelete.Visibility = Visibility.Visible;
                    btnEdit.Visibility = Visibility.Visible;
                }

                if (expExcludedPaths.IsExpanded
                    && lbxExcludedPaths.Items.Count > 0
                    && btnDelete.Visibility == Visibility.Hidden
                    && btnEdit.Visibility == Visibility.Hidden)
                {
                    btnDelete.Visibility = Visibility.Visible;
                    btnEdit.Visibility = Visibility.Visible;
                }
            }

            lbxFolderLinks.Items.Refresh();

            lbxExcludedPaths.Items.Refresh();

            lbxActionKeywords.Items.Refresh();
        }

        private void expActionKeywords_Click(object sender, RoutedEventArgs e)
        {
            if (expActionKeywords.IsExpanded)
                expActionKeywords.Height = 215;

            if (expExcludedPaths.IsExpanded)
                expExcludedPaths.IsExpanded = false;

            if (expFolderLinks.IsExpanded)
                expFolderLinks.IsExpanded = false;

            RefreshView();
        }

        private void expActionKeywords_Collapsed(object sender, RoutedEventArgs e)
        {
            if (!expActionKeywords.IsExpanded)
                expActionKeywords.Height = Double.NaN;
        }

        private void expFolderLinks_Click(object sender, RoutedEventArgs e)
        {
            if (expFolderLinks.IsExpanded)
                expFolderLinks.Height = 215;

            if (expExcludedPaths.IsExpanded)
                expExcludedPaths.IsExpanded = false;

            if (expActionKeywords.IsExpanded)
                expActionKeywords.IsExpanded = false;
            
            RefreshView();
        }

        private void expFolderLinks_Collapsed(object sender, RoutedEventArgs e)
        {
            if (!expFolderLinks.IsExpanded)
                expFolderLinks.Height = Double.NaN;
        }

        private void expExcludedPaths_Click(object sender, RoutedEventArgs e)
        {
            if (expExcludedPaths.IsExpanded)
                expFolderLinks.Height = Double.NaN;

            if (expFolderLinks.IsExpanded)
                expFolderLinks.IsExpanded = false;

            if (expActionKeywords.IsExpanded)
                expActionKeywords.IsExpanded = false;

            RefreshView();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = lbxFolderLinks.SelectedItem as AccessLink?? lbxExcludedPaths.SelectedItem as AccessLink;

            if (selectedRow != null)
            {
                string msg = string.Format(viewModel.Context.API.GetTranslation("plugin_explorer_delete_folder_link"), selectedRow.Path);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (expFolderLinks.IsExpanded)
                        viewModel.RemoveFolderLinkFromQuickFolders(selectedRow);

                    if (expExcludedPaths.IsExpanded)
                        viewModel.RemoveFolderLinkFromExcludedIndexPaths(selectedRow);

                    RefreshView();
                }
            }
            else
            {
                string warning = viewModel.Context.API.GetTranslation("plugin_explorer_select_folder_link_warning");
                MessageBox.Show(warning);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (lbxActionKeywords.SelectedItem is ActionKeywordView)
            {
                var selectedActionKeyword = lbxActionKeywords.SelectedItem as ActionKeywordView;

                var actionKeywordWindow = new ActionKeywordSetting(viewModel, actionKeywordsListView, selectedActionKeyword);

                actionKeywordWindow.ShowDialog();

                RefreshView();
            }
            else
            {
                var selectedRow = lbxFolderLinks.SelectedItem as AccessLink ?? lbxExcludedPaths.SelectedItem as AccessLink;

                if (selectedRow != null)
                {
                    var folderBrowserDialog = new FolderBrowserDialog();
                    folderBrowserDialog.SelectedPath = selectedRow.Path;
                    if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                    {
                        if (expFolderLinks.IsExpanded)
                        {
                            var link = viewModel.Settings.QuickFolderAccessLinks.First(x => x.Path == selectedRow.Path);
                            link.Path = folderBrowserDialog.SelectedPath;
                        }

                        if (expExcludedPaths.IsExpanded)
                        {
                            var link = viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.First(x => x.Path == selectedRow.Path);
                            link.Path = folderBrowserDialog.SelectedPath;
                        }
                    }

                    RefreshView();
                }
                else
                {
                    string warning = viewModel.Context.API.GetTranslation("plugin_explorer_make_selection_warning");
                    MessageBox.Show(warning);
                }
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                var newFolderLink = new AccessLink
                {
                    Path = folderBrowserDialog.SelectedPath
                };

                AddFolderLink(newFolderLink);
            }

            RefreshView();
        }

        private void lbxFolders_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Count() > 0)
            {
                if (expFolderLinks.IsExpanded && viewModel.Settings.QuickFolderAccessLinks == null)
                    viewModel.Settings.QuickFolderAccessLinks = new List<AccessLink>();

                foreach (string s in files)
                {
                    if (Directory.Exists(s))
                    {
                        var newFolderLink = new AccessLink
                        {
                            Path = s
                        };

                        AddFolderLink(newFolderLink);
                    }

                    RefreshView();
                }
            }
        }

        private void AddFolderLink(AccessLink newFolderLink)
        {
            if (expFolderLinks.IsExpanded
                    && !viewModel.Settings.QuickFolderAccessLinks.Any(x => x.Path == newFolderLink.Path))
            {
                if (viewModel.Settings.QuickFolderAccessLinks == null)
                    viewModel.Settings.QuickFolderAccessLinks = new List<AccessLink>();

                viewModel.Settings.QuickFolderAccessLinks.Add(newFolderLink);
            }

            if (expExcludedPaths.IsExpanded
                && !viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.Any(x => x.Path == newFolderLink.Path))
            {
                if (viewModel.Settings.IndexSearchExcludedSubdirectoryPaths == null)
                    viewModel.Settings.IndexSearchExcludedSubdirectoryPaths = new List<AccessLink>();

                viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.Add(newFolderLink);
            }
        }

        private void lbxFolders_DragEnter(object sender, DragEventArgs e)
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
            viewModel.OpenWindowsIndexingOptions();
        }
    }

    public class ActionKeywordView
    {
        public string Description { get; set; }

        public string Keyword { get; set; }
    }
}
