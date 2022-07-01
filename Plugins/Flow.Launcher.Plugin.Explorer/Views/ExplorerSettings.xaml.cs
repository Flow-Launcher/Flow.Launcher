using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
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

        private List<ActionKeywordModel> actionKeywordsListView;

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            DataContext = viewModel;

            InitializeComponent();

            this.viewModel = viewModel;

            lbxAccessLinks.ItemsSource = this.viewModel.Settings.QuickAccessLinks;

            lbxExcludedPaths.ItemsSource = this.viewModel.Settings.IndexSearchExcludedSubdirectoryPaths;

            actionKeywordsListView = new List<ActionKeywordModel>
            {
                new(Settings.ActionKeyword.SearchActionKeyword,
                    viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_search")),
                new(Settings.ActionKeyword.FileContentSearchActionKeyword,
                    viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_filecontentsearch")),
                new(Settings.ActionKeyword.PathSearchActionKeyword,
                    viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_pathsearch")),
                new(Settings.ActionKeyword.IndexSearchActionKeyword,
                    viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_indexsearch")),
                new(Settings.ActionKeyword.QuickAccessActionKeyword,
                    viewModel.Context.API.GetTranslation("plugin_explorer_actionkeywordview_quickaccess"))
            };

            lbxActionKeywords.ItemsSource = actionKeywordsListView;

            ActionKeywordModel.Init(viewModel.Settings);

            lbxAccessLinks.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));

            lbxExcludedPaths.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));
        }
 

        private void expActionKeywords_Click(object sender, RoutedEventArgs e)
        {
            if (expExcludedPaths.IsExpanded)
                expExcludedPaths.IsExpanded = false;

            if (expAccessLinks.IsExpanded)
                expAccessLinks.IsExpanded = false;
        }


        private void expAccessLinks_Click(object sender, RoutedEventArgs e)
        {
            if (expExcludedPaths.IsExpanded)
                expExcludedPaths.IsExpanded = false;

            if (expActionKeywords.IsExpanded)
                expActionKeywords.IsExpanded = false;
        }

        private void expExcludedPaths_Click(object sender, RoutedEventArgs e)
        {
            if (expAccessLinks.IsExpanded)
                expAccessLinks.IsExpanded = false;

            if (expActionKeywords.IsExpanded)
                expActionKeywords.IsExpanded = false;
        }


        private void lbxAccessLinks_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Count() > 0)
            {
                if (expAccessLinks.IsExpanded && viewModel.Settings.QuickAccessLinks == null)
                    viewModel.Settings.QuickAccessLinks = new();

                foreach (string s in files)
                {
                    if (Directory.Exists(s))
                    {
                        var newFolderLink = new AccessLink { Path = s };

                        AddAccessLink(newFolderLink);
                    }
                }
            }
        }

        private void AddAccessLink(AccessLink newAccessLink)
        {
            if (expAccessLinks.IsExpanded
                && !viewModel.Settings.QuickAccessLinks.Any(x => x.Path == newAccessLink.Path))
            {
                if (viewModel.Settings.QuickAccessLinks == null)
                    viewModel.Settings.QuickAccessLinks = new();

                viewModel.Settings.QuickAccessLinks.Add(newAccessLink);
            }

            if (expExcludedPaths.IsExpanded
                && !viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.Any(x => x.Path == newAccessLink.Path))
            {
                if (viewModel.Settings.IndexSearchExcludedSubdirectoryPaths == null)
                    viewModel.Settings.IndexSearchExcludedSubdirectoryPaths = new ();

                viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.Add(newAccessLink);
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
    }
}