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

        private List<ActionKeywordView> actionKeywordsListView;

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            
            InitializeComponent();

            this.viewModel = viewModel;

            lbxAccessLinks.ItemsSource = this.viewModel.Settings.QuickAccessLinks;

            lbxExcludedPaths.ItemsSource = this.viewModel.Settings.IndexSearchExcludedSubdirectoryPaths;

            actionKeywordsListView = new List<ActionKeywordView>
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

            ActionKeywordView.Init(viewModel.Settings);

            RefreshView();
        }

        public void RefreshView()
        {
            lbxAccessLinks.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));

            lbxExcludedPaths.Items.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Ascending));

            SetButtonVisibilityToHidden();

            if (expAccessLinks.IsExpanded || expExcludedPaths.IsExpanded || expActionKeywords.IsExpanded)
            {
                if (!expActionKeywords.IsExpanded)
                    btnAdd.Visibility = Visibility.Visible;

                if (expActionKeywords.IsExpanded
                    && btnEdit.Visibility == Visibility.Hidden)
                    btnEdit.Visibility = Visibility.Visible;

                if (lbxAccessLinks.Items.Count == 0 && lbxExcludedPaths.Items.Count == 0
                                                    && btnDelete.Visibility == Visibility.Visible
                                                    && btnEdit.Visibility == Visibility.Visible)
                {
                    btnDelete.Visibility = Visibility.Hidden;
                    btnEdit.Visibility = Visibility.Hidden;
                }

                if (expAccessLinks.IsExpanded
                    && lbxAccessLinks.Items.Count > 0
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

            lbxAccessLinks.Items.Refresh();

            lbxExcludedPaths.Items.Refresh();

            lbxActionKeywords.Items.Refresh();
        }

        private void expActionKeywords_Click(object sender, RoutedEventArgs e)
        {
            if (expActionKeywords.IsExpanded)
                expActionKeywords.Height = 205;

            if (expExcludedPaths.IsExpanded)
                expExcludedPaths.IsExpanded = false;

            if (expAccessLinks.IsExpanded)
                expAccessLinks.IsExpanded = false;

            RefreshView();
        }

        private void expActionKeywords_Collapsed(object sender, RoutedEventArgs e)
        {
            expActionKeywords.Height = double.NaN;
            SetButtonVisibilityToHidden();
        }

        private void expAccessLinks_Click(object sender, RoutedEventArgs e)
        {
            if (expAccessLinks.IsExpanded)
                expAccessLinks.Height = 205;

            if (expExcludedPaths.IsExpanded)
                expExcludedPaths.IsExpanded = false;

            if (expActionKeywords.IsExpanded)
                expActionKeywords.IsExpanded = false;

            RefreshView();
        }

        private void expAccessLinks_Collapsed(object sender, RoutedEventArgs e)
        {
            expAccessLinks.Height = double.NaN;
            SetButtonVisibilityToHidden();
        }

        private void expExcludedPaths_Click(object sender, RoutedEventArgs e)
        {
            if (expExcludedPaths.IsExpanded)
                expAccessLinks.Height = double.NaN;

            if (expAccessLinks.IsExpanded)
                expAccessLinks.IsExpanded = false;

            if (expActionKeywords.IsExpanded)
                expActionKeywords.IsExpanded = false;

            RefreshView();
        }

        private void expExcludedPaths_Collapsed(object sender, RoutedEventArgs e)
        {
            SetButtonVisibilityToHidden();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = lbxAccessLinks.SelectedItem as AccessLink ?? lbxExcludedPaths.SelectedItem as AccessLink;

            if (selectedRow != null)
            {
                string msg = string.Format(viewModel.Context.API.GetTranslation("plugin_explorer_delete_folder_link"),
                    selectedRow.Path);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (expAccessLinks.IsExpanded)
                        viewModel.RemoveLinkFromQuickAccess(selectedRow);

                    if (expExcludedPaths.IsExpanded)
                        viewModel.RemoveAccessLinkFromExcludedIndexPaths(selectedRow);

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

                var actionKeywordWindow = new ActionKeywordSetting(viewModel,
                    selectedActionKeyword);

                actionKeywordWindow.ShowDialog();

                RefreshView();
            }
            else
            {
                var selectedRow = lbxAccessLinks.SelectedItem as AccessLink ??
                                  lbxExcludedPaths.SelectedItem as AccessLink;

                if (selectedRow != null)
                {
                    var folderBrowserDialog = new FolderBrowserDialog();
                    folderBrowserDialog.SelectedPath = selectedRow.Path;
                    if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                    {
                        if (expAccessLinks.IsExpanded)
                        {
                            var link = viewModel.Settings.QuickAccessLinks.First(x => x.Path == selectedRow.Path);
                            link.Path = folderBrowserDialog.SelectedPath;
                        }

                        if (expExcludedPaths.IsExpanded)
                        {
                            var link = viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.First(x =>
                                x.Path == selectedRow.Path);
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
                var newAccessLink = new AccessLink {Path = folderBrowserDialog.SelectedPath};

                AddAccessLink(newAccessLink);
            }

            RefreshView();
        }

        private void lbxAccessLinks_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Count() > 0)
            {
                if (expAccessLinks.IsExpanded && viewModel.Settings.QuickAccessLinks == null)
                    viewModel.Settings.QuickAccessLinks = new List<AccessLink>();

                foreach (string s in files)
                {
                    if (Directory.Exists(s))
                    {
                        var newFolderLink = new AccessLink {Path = s};

                        AddAccessLink(newFolderLink);
                    }

                    RefreshView();
                }
            }
        }

        private void AddAccessLink(AccessLink newAccessLink)
        {
            if (expAccessLinks.IsExpanded
                && !viewModel.Settings.QuickAccessLinks.Any(x => x.Path == newAccessLink.Path))
            {
                if (viewModel.Settings.QuickAccessLinks == null)
                    viewModel.Settings.QuickAccessLinks = new List<AccessLink>();

                viewModel.Settings.QuickAccessLinks.Add(newAccessLink);
            }

            if (expExcludedPaths.IsExpanded
                && !viewModel.Settings.IndexSearchExcludedSubdirectoryPaths.Any(x => x.Path == newAccessLink.Path))
            {
                if (viewModel.Settings.IndexSearchExcludedSubdirectoryPaths == null)
                    viewModel.Settings.IndexSearchExcludedSubdirectoryPaths = new List<AccessLink>();

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

        public void SetButtonVisibilityToHidden()
        {
            btnDelete.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Hidden;
            btnAdd.Visibility = Visibility.Hidden;
        }
    }

    public class ActionKeywordView
    {
        private static Settings _settings;

        public static void Init(Settings settings)
        {
            _settings = settings;
        }

        internal ActionKeywordView(Settings.ActionKeyword actionKeyword, string description)
        {
            KeywordProperty = actionKeyword;
            Description = description;
        }

        public string Description { get; private init; }

        internal Settings.ActionKeyword KeywordProperty { get; }

        public string Keyword
        {
            get => _settings.GetActionKeyword(KeywordProperty);
            set => _settings.SetActionKeyword(KeywordProperty, value);
        }

        public bool Enabled
        {
            get => _settings.GetActionKeywordEnabled(KeywordProperty);
            set => _settings.SetActionKeywordEnabled(KeywordProperty, value);
        }
    }
}