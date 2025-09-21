using Droplex;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything;

public static class EverythingDownloadHelper
{
    public static async Task<string> PromptDownloadIfNotInstallAsync(string installedLocation, IPublicAPI api)
    {
        if (!string.IsNullOrEmpty(installedLocation) && installedLocation.FileExists())
            return installedLocation;

        installedLocation = GetInstalledPath();

        if (string.IsNullOrEmpty(installedLocation))
        {
            if (api.ShowMsgBox(
                Localize.flowlauncher_plugin_everything_installing_select(Environment.NewLine),
                Localize.flowlauncher_plugin_everything_installing_title(),
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var dlg = new System.Windows.Forms.OpenFileDialog
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                };

                var result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.FileName))
                    installedLocation = dlg.FileName;
            }
        }

        if (!string.IsNullOrEmpty(installedLocation))
        {
            return installedLocation;
        }

        api.ShowMsg(Localize.flowlauncher_plugin_everything_installing_title(),
            Localize.flowlauncher_plugin_everything_installing_subtitle(), "", useMainWindowAsOwner: false);

        await DroplexPackage.Drop(App.Everything1_4_1_1009).ConfigureAwait(false);

        api.ShowMsg(Localize.flowlauncher_plugin_everything_installing_title(),
            Localize.flowlauncher_plugin_everything_installationsuccess_subtitle(), "", useMainWindowAsOwner: false);

        installedLocation = "C:\\Program Files\\Everything\\Everything.exe";

        FilesFolders.OpenPath(installedLocation, (string str) => api.ShowMsgBox(str));

        return installedLocation;

    }

    internal static string GetInstalledPath()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (key is not null)
        {
            foreach (var subKey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
            {
                if (subKey?.GetValue("DisplayName") is not string displayName || !displayName.Contains("Everything"))
                {
                    continue;
                }
                if (subKey.GetValue("UninstallString") is not string uninstallString)
                {
                    continue;
                }

                if (Path.GetDirectoryName(uninstallString) is not { } uninstallDirectory)
                {
                    continue;
                }
                return Path.Combine(uninstallDirectory, "Everything.exe");
            }
        }

        var scoopInstalledPath = Environment.ExpandEnvironmentVariables(@"%userprofile%\scoop\apps\everything\current\Everything.exe");
        return File.Exists(scoopInstalledPath) ? scoopInstalledPath : string.Empty;
    }
}
