using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Helper;

/// <summary>
/// Helper class for installing, updating, and uninstalling plugins.
/// </summary>
public static class PluginInstallationHelper
{
    private static readonly string ClassName = nameof(PluginInstallationHelper);

    private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

    public static async Task InstallPluginAndCheckRestartAsync(UserPlugin newPlugin)
    {
        if (App.API.ShowMsgBox(
            string.Format(
                App.API.GetTranslation("InstallPromptSubtitle"),
                newPlugin.Name, newPlugin.Author, Environment.NewLine),
            App.API.GetTranslation("InstallPromptTitle"),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            // at minimum should provide a name, but handle plugin that is not downloaded from plugins manifest and is a url download
            var downloadFilename = string.IsNullOrEmpty(newPlugin.Version)
                ? $"{newPlugin.Name}-{Guid.NewGuid()}.zip"
                : $"{newPlugin.Name}-{newPlugin.Version}.zip";

            var filePath = Path.Combine(Path.GetTempPath(), downloadFilename);

            using var cts = new CancellationTokenSource();

            if (!newPlugin.IsFromLocalInstallPath)
            {
                await DownloadFileAsync(
                    $"{App.API.GetTranslation("DownloadingPlugin")} {newPlugin.Name}",
                    newPlugin.UrlDownload, filePath, cts);
            }
            else
            {
                filePath = newPlugin.LocalInstallPath;
            }

            // check if user cancelled download before installing plugin
            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Plugin {newPlugin.ID} zip file not found at {filePath}", filePath);
            }

            App.API.InstallPlugin(newPlugin, filePath);

            if (!newPlugin.IsFromLocalInstallPath)
            {
                File.Delete(filePath);
            }
        }
        catch (Exception e)
        {
            App.API.LogException(ClassName, "Failed to install plugin", e);
            App.API.ShowMsgError(App.API.GetTranslation("ErrorInstallingPlugin"));
            return; // don’t restart on failure
        }

        if (Settings.AutoRestartAfterChanging)
        {
            App.API.RestartApp();
        }
        else
        {
            App.API.ShowMsg(
                App.API.GetTranslation("installbtn"),
                string.Format(
                    App.API.GetTranslation(
                        "InstallSuccessNoRestart"),
                    newPlugin.Name));
        }
    }

    public static async Task InstallPluginAndCheckRestartAsync(string filePath)
    {
        UserPlugin plugin;
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(filePath);
            var pluginJsonEntry = archive.Entries.FirstOrDefault(x => x.Name == "plugin.json") ??
                throw new FileNotFoundException("The zip file does not contain a plugin.json file.");

            using Stream stream = pluginJsonEntry.Open();
            plugin = JsonSerializer.Deserialize<UserPlugin>(stream);
            plugin.IcoPath = "Images\\zipfolder.png";
            plugin.LocalInstallPath = filePath;
        }
        catch (Exception e)
        {
            App.API.LogException(ClassName, "Failed to validate zip file", e);
            App.API.ShowMsgError(App.API.GetTranslation("ZipFileNotHavePluginJson"));
            return;
        }

        if (Settings.ShowUnknownSourceWarning)
        {
            if (!InstallSourceKnown(plugin.Website)
                && App.API.ShowMsgBox(string.Format(
                    App.API.GetTranslation("InstallFromUnknownSourceSubtitle"), Environment.NewLine),
                    App.API.GetTranslation("InstallFromUnknownSourceTitle"),
                    MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;
        }

        await InstallPluginAndCheckRestartAsync(plugin);
    }

    public static async Task UninstallPluginAndCheckRestartAsync(PluginMetadata oldPlugin)
    {
        if (App.API.ShowMsgBox(
            string.Format(
                App.API.GetTranslation("UninstallPromptSubtitle"),
                oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
            App.API.GetTranslation("UninstallPromptTitle"),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        var removePluginSettings = App.API.ShowMsgBox(
            App.API.GetTranslation("KeepPluginSettingsSubtitle"),
            App.API.GetTranslation("KeepPluginSettingsTitle"),
            button: MessageBoxButton.YesNo) == MessageBoxResult.No;

        try
        {
            await App.API.UninstallPluginAsync(oldPlugin, removePluginSettings);
        }
        catch (Exception e)
        {
            App.API.LogException(ClassName, "Failed to uninstall plugin", e);
            App.API.ShowMsgError(App.API.GetTranslation("ErrorUninstallingPlugin"));
            return; // don’t restart on failure
        }

        if (Settings.AutoRestartAfterChanging)
        {
            App.API.RestartApp();
        }
        else
        {
            App.API.ShowMsg(
                App.API.GetTranslation("uninstallbtn"),
                string.Format(
                    App.API.GetTranslation(
                        "UninstallSuccessNoRestart"),
                    oldPlugin.Name));
        }
    }

    public static async Task UpdatePluginAndCheckRestartAsync(UserPlugin newPlugin, PluginMetadata oldPlugin)
    {
        if (App.API.ShowMsgBox(
            string.Format(
                App.API.GetTranslation("UpdatePromptSubtitle"),
                oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
            App.API.GetTranslation("UpdatePromptTitle"),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{newPlugin.Name}-{newPlugin.Version}.zip");

            using var cts = new CancellationTokenSource();

            if (!newPlugin.IsFromLocalInstallPath)
            {
                await DownloadFileAsync(
                    $"{App.API.GetTranslation("DownloadingPlugin")} {newPlugin.Name}",
                    newPlugin.UrlDownload, filePath, cts);
            }
            else
            {
                filePath = newPlugin.LocalInstallPath;
            }

            // check if user cancelled download before installing plugin
            if (cts.IsCancellationRequested)
            {
                return;
            }

            await App.API.UpdatePluginAsync(oldPlugin, newPlugin, filePath);
        }
        catch (Exception e)
        {
            App.API.LogException(ClassName, "Failed to update plugin", e);
            App.API.ShowMsgError(App.API.GetTranslation("ErrorUpdatingPlugin"));
            return; // don’t restart on failure
        }

        if (Settings.AutoRestartAfterChanging)
        {
            App.API.RestartApp();
        }
        else
        {
            App.API.ShowMsg(
                App.API.GetTranslation("updatebtn"),
                string.Format(
                    App.API.GetTranslation(
                        "UpdateSuccessNoRestart"),
                    newPlugin.Name));
        }
    }

    private static async Task DownloadFileAsync(string prgBoxTitle, string downloadUrl, string filePath, CancellationTokenSource cts, bool deleteFile = true, bool showProgress = true)
    {
        if (deleteFile && File.Exists(filePath))
            File.Delete(filePath);

        if (showProgress)
        {
            var exceptionHappened = false;
            await App.API.ShowProgressBoxAsync(prgBoxTitle,
                async (reportProgress) =>
                {
                    if (reportProgress == null)
                    {
                        // when reportProgress is null, it means there is expcetion with the progress box
                        // so we record it with exceptionHappened and return so that progress box will close instantly
                        exceptionHappened = true;
                        return;
                    }
                    else
                    {
                        await App.API.HttpDownloadAsync(downloadUrl, filePath, reportProgress, cts.Token).ConfigureAwait(false);
                    }
                }, cts.Cancel);

            // if exception happened while downloading and user does not cancel downloading,
            // we need to redownload the plugin
            if (exceptionHappened && (!cts.IsCancellationRequested))
                await App.API.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
        }
        else
        {
            await App.API.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
        }
    }

    private static bool InstallSourceKnown(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        var pieces = url.Split('/');

        if (pieces.Length < 4)
            return false;

        var author = pieces[3];
        var acceptedSource = "https://github.com";
        var constructedUrlPart = string.Format("{0}/{1}/", acceptedSource, author);

        return url.StartsWith(acceptedSource) &&
            App.API.GetAllPlugins().Any(x =>
                !string.IsNullOrEmpty(x.Metadata.Website) &&
                x.Metadata.Website.StartsWith(constructedUrlPart)
            );
    }
}
