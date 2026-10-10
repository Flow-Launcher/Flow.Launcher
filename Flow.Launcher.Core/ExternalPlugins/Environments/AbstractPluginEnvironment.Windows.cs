using System;
using System.Windows.Forms;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    public abstract partial class AbstractPluginEnvironment
    {
        partial void EnsureLatestInstalled(string expectedPath, string currentPath, string installedDirPath)
        {
            if (expectedPath == currentPath) return;

            FilesFolders.RemoveFolderIfExists(installedDirPath, (s) => API.ShowMsgBox(s));

            InstallEnvironment();
        }

        private static partial string GetFileFromDialog(string title, string filter)
        {
            var dlg = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Title = title,
                Filter = filter
            };

            var result = dlg.ShowDialog();
            return result == DialogResult.OK ? dlg.FileName : string.Empty;
        }
    }
}
