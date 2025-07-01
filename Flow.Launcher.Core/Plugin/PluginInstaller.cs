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

namespace Flow.Launcher.Core.Plugin;

/// <summary>
/// Helper class for installing, updating, and uninstalling plugins.
/// </summary>
public static class PluginInstaller
{
    private static readonly string ClassName = nameof(PluginInstaller);

    private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

    // We should not initialize API in static constructor because it will create another API instance
    private static IPublicAPI api = null;
    private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

    public static async Task InstallPluginAndCheckRestartAsync(UserPlugin newPlugin)
    {
        if (API.PluginModified(newPlugin.ID))
        {
            API.ShowMsgError(string.Format(API.GetTranslation("failedToInstallPluginTitle"), newPlugin.Name),
                    API.GetTranslation("pluginModifiedAlreadyMessage"));
            return;
        }

        if (API.ShowMsgBox(
            string.Format(
                API.GetTranslation("InstallPromptSubtitle"),
                newPlugin.Name, newPlugin.Author, Environment.NewLine),
            API.GetTranslation("InstallPromptTitle"),
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
                    $"{API.GetTranslation("DownloadingPlugin")} {newPlugin.Name}",
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

            API.InstallPlugin(newPlugin, filePath);

            if (!newPlugin.IsFromLocalInstallPath)
            {
                File.Delete(filePath);
            }
        }
        catch (Exception e)
        {
            API.LogException(ClassName, "Failed to install plugin", e);
            API.ShowMsgError(API.GetTranslation("ErrorInstallingPlugin"));
            return; // do not restart on failure
        }

        if (Settings.AutoRestartAfterChanging)
        {
            API.RestartApp();
        }
        else
        {
            API.ShowMsg(
                API.GetTranslation("installbtn"),
                string.Format(
                    API.GetTranslation(
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
            API.LogException(ClassName, "Failed to validate zip file", e);
            API.ShowMsgError(API.GetTranslation("ZipFileNotHavePluginJson"));
            return;
        }

        if (API.PluginModified(plugin.ID))
        {
            API.ShowMsgError(string.Format(API.GetTranslation("failedToInstallPluginTitle"), plugin.Name),
                    API.GetTranslation("pluginModifiedAlreadyMessage"));
            return;
        }

        if (Settings.ShowUnknownSourceWarning)
        {
            if (!InstallSourceKnown(plugin.Website)
                && API.ShowMsgBox(string.Format(
                    API.GetTranslation("InstallFromUnknownSourceSubtitle"), Environment.NewLine),
                    API.GetTranslation("InstallFromUnknownSourceTitle"),
                    MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;
        }

        await InstallPluginAndCheckRestartAsync(plugin);
    }

    public static async Task UninstallPluginAndCheckRestartAsync(PluginMetadata oldPlugin)
    {
        if (API.PluginModified(oldPlugin.ID))
        {
            API.ShowMsgError(string.Format(API.GetTranslation("failedToUninstallPluginTitle"), oldPlugin.Name),
                    API.GetTranslation("pluginModifiedAlreadyMessage"));
            return;
        }

        if (API.ShowMsgBox(
            string.Format(
                API.GetTranslation("UninstallPromptSubtitle"),
                oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
            API.GetTranslation("UninstallPromptTitle"),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        var removePluginSettings = API.ShowMsgBox(
            API.GetTranslation("KeepPluginSettingsSubtitle"),
            API.GetTranslation("KeepPluginSettingsTitle"),
            button: MessageBoxButton.YesNo) == MessageBoxResult.No;

        try
        {
            await API.UninstallPluginAsync(oldPlugin, removePluginSettings);
        }
        catch (Exception e)
        {
            API.LogException(ClassName, "Failed to uninstall plugin", e);
            API.ShowMsgError(API.GetTranslation("ErrorUninstallingPlugin"));
            return; // don not restart on failure
        }

        if (Settings.AutoRestartAfterChanging)
        {
            API.RestartApp();
        }
        else
        {
            API.ShowMsg(
                API.GetTranslation("uninstallbtn"),
                string.Format(
                    API.GetTranslation(
                        "UninstallSuccessNoRestart"),
                    oldPlugin.Name));
        }
    }

    public static async Task UpdatePluginAndCheckRestartAsync(UserPlugin newPlugin, PluginMetadata oldPlugin)
    {
        if (API.ShowMsgBox(
            string.Format(
                API.GetTranslation("UpdatePromptSubtitle"),
                oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
            API.GetTranslation("UpdatePromptTitle"),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{newPlugin.Name}-{newPlugin.Version}.zip");

            using var cts = new CancellationTokenSource();

            if (!newPlugin.IsFromLocalInstallPath)
            {
                await DownloadFileAsync(
                    $"{API.GetTranslation("DownloadingPlugin")} {newPlugin.Name}",
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

            await API.UpdatePluginAsync(oldPlugin, newPlugin, filePath);
        }
        catch (Exception e)
        {
            API.LogException(ClassName, "Failed to update plugin", e);
            API.ShowMsgError(API.GetTranslation("ErrorUpdatingPlugin"));
            return; // do not restart on failure
        }

        if (Settings.AutoRestartAfterChanging)
        {
            API.RestartApp();
        }
        else
        {
            API.ShowMsg(
                API.GetTranslation("updatebtn"),
                string.Format(
                    API.GetTranslation(
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
            await API.ShowProgressBoxAsync(prgBoxTitle,
                async (reportProgress) =>
                {
                    if (reportProgress == null)
                    {
                        // when reportProgress is null, it means there is exception with the progress box
                        // so we record it with exceptionHappened and return so that progress box will close instantly
                        exceptionHappened = true;
                        return;
                    }
                    else
                    {
                        await API.HttpDownloadAsync(downloadUrl, filePath, reportProgress, cts.Token).ConfigureAwait(false);
                    }
                }, cts.Cancel);

            // if exception happened while downloading and user does not cancel downloading,
            // we need to redownload the plugin
            if (exceptionHappened && (!cts.IsCancellationRequested))
                await API.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
        }
        else
        {
            await API.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
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
        var acceptedHost = "github.com";
        var acceptedSource = "https://github.com";
        var constructedUrlPart = string.Format("{0}/{1}/", acceptedSource, author);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != acceptedHost)
            return false;

        return API.GetAllPlugins().Any(x =>
            !string.IsNullOrEmpty(x.Metadata.Website) &&
            x.Metadata.Website.StartsWith(constructedUrlPart)
        );
    }
}
