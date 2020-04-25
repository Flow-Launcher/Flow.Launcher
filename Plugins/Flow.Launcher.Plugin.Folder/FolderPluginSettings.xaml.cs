using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Flow.Launcher.Plugin.Folder
{

    public partial class FileSystemSettings
    {
        private IPublicAPI flowlauncherAPI;
        private Settings _settings;

        public FileSystemSettings(IPublicAPI flowlauncherAPI, Settings settings)
        {
            this.flowlauncherAPI = flowlauncherAPI;
            InitializeComponent();
            _settings = settings;
            lbxFolders.ItemsSource = _settings.FolderLinks;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = lbxFolders.SelectedItem as FolderLink;
            if (selectedFolder != null)
            {
                string msg = string.Format(flowlauncherAPI.GetTranslation("flowlauncher_plugin_folder_delete_folder_link"), selectedFolder.Path);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _settings.FolderLinks.Remove(selectedFolder);
                    lbxFolders.Items.Refresh();
                }
            }
            else
            {
                string warning = flowlauncherAPI.GetTranslation("flowlauncher_plugin_folder_select_folder_link_warning");
                MessageBox.Show(warning);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = lbxFolders.SelectedItem as FolderLink;
            if (selectedFolder != null)
            {
                var folderBrowserDialog = new FolderBrowserDialog();
                folderBrowserDialog.SelectedPath = selectedFolder.Path;
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    var link = _settings.FolderLinks.First(x => x.Path == selectedFolder.Path);
                    link.Path = folderBrowserDialog.SelectedPath;
                }

                lbxFolders.Items.Refresh();
            }
            else
            {
                string warning = flowlauncherAPI.GetTranslation("flowlauncher_plugin_folder_select_folder_link_warning");
                MessageBox.Show(warning);
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                var newFolder = new FolderLink
                {
                    Path = folderBrowserDialog.SelectedPath
                };

                if (_settings.FolderLinks == null)
                {
                    _settings.FolderLinks = new List<FolderLink>();
                }

                _settings.FolderLinks.Add(newFolder);
            }

            lbxFolders.Items.Refresh();
        }

        private void lbxFolders_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Count() > 0)
            {
                if (_settings.FolderLinks == null)
                {
                    _settings.FolderLinks = new List<FolderLink>();
                }

                foreach (string s in files)
                {
                    if (Directory.Exists(s))
                    {
                        var newFolder = new FolderLink
                        {
                            Path = s
                        };

                        _settings.FolderLinks.Add(newFolder);
                    }

                    lbxFolders.Items.Refresh();
                }
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
    }
}
