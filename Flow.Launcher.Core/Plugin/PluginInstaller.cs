using System;
using System.Collections.Generic;
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
/// Class for installing, updating, and uninstalling plugins.
/// </summary>
public static class PluginInstaller
{
    private static readonly string ClassName = nameof(PluginInstaller);

    private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

    // We should not initialize API in static constructor because it will create another API instance
    private static IPublicAPI api = null;
    private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

    /// <summary>
    /// Installs a plugin and restarts the application if required by settings. Prompts user for confirmation and handles download if needed.
    /// </summary>
    /// <param name="newPlugin">The plugin to install.</param>
    /// <returns>A Task representing the asynchronous install operation.</returns>
    public static async Task InstallPluginAndCheckRestartAsync(UserPlugin newPlugin)
    {
        if (API.PluginModified(newPlugin.ID))
        {
            API.ShowMsgError(string.Format(API.GetTranslation("pluginModifiedAlreadyTitle"), newPlugin.Name),
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

            if (!API.InstallPlugin(newPlugin, filePath))
            {
                return;
            }

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

    /// <summary>
    /// Installs a plugin from a local zip file and restarts the application if required by settings. Validates the zip and prompts user for confirmation.
    /// </summary>
    /// <param name="filePath">The path to the plugin zip file.</param>
    /// <returns>A Task representing the asynchronous install operation.</returns>
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
            API.ShowMsgError(string.Format(API.GetTranslation("pluginModifiedAlreadyTitle"), plugin.Name),
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

    /// <summary>
    /// Uninstalls a plugin and restarts the application if required by settings. Prompts user for confirmation and whether to keep plugin settings.
    /// </summary>
    /// <param name="oldPlugin">The plugin metadata to uninstall.</param>
    /// <returns>A Task representing the asynchronous uninstall operation.</returns>
    public static async Task UninstallPluginAndCheckRestartAsync(PluginMetadata oldPlugin)
    {
        if (API.PluginModified(oldPlugin.ID))
        {
            API.ShowMsgError(string.Format(API.GetTranslation("pluginModifiedAlreadyTitle"), oldPlugin.Name),
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
            if (!await API.UninstallPluginAsync(oldPlugin, removePluginSettings))
            {
                return;
            }
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

    /// <summary>
    /// Updates a plugin to a new version and restarts the application if required by settings. Prompts user for confirmation and handles download if needed.
    /// </summary>
    /// <param name="newPlugin">The new plugin version to install.</param>
    /// <param name="oldPlugin">The existing plugin metadata to update.</param>
    /// <returns>A Task representing the asynchronous update operation.</returns>
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

            if (!await API.UpdatePluginAsync(oldPlugin, newPlugin, filePath))
            {
                return;
            }
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

    /// <summary>
    /// Updates the plugin to the latest version available from its source.
    /// </summary>
    /// <param name="silentUpdate">If true, do not show any messages when there is no udpate available.</param>
    /// <param name="usePrimaryUrlOnly">If true, only use the primary URL for updates.</param>
    /// <param name="token">Cancellation token to cancel the update operation.</param>
    /// <returns></returns>
    public static async Task CheckForPluginUpdatesAsync(bool silentUpdate = true, bool usePrimaryUrlOnly = false, CancellationToken token = default)
    {
        // Update the plugin manifest
        await API.UpdatePluginManifestAsync(usePrimaryUrlOnly, token);

        // Get all plugins that can be updated
        var resultsForUpdate = (
            from existingPlugin in API.GetAllPlugins()
            join pluginUpdateSource in API.GetPluginManifest()
                on existingPlugin.Metadata.ID equals pluginUpdateSource.ID
            where string.Compare(existingPlugin.Metadata.Version, pluginUpdateSource.Version,
                      StringComparison.InvariantCulture) <
                  0 // if current version precedes version of the plugin from update source (e.g. PluginsManifest)
                  && !API.PluginModified(existingPlugin.Metadata.ID)
            select
                new PluginUpdateInfo()
                {
                    ID = existingPlugin.Metadata.ID,
                    Name = existingPlugin.Metadata.Name,
                    Author = existingPlugin.Metadata.Author,
                    CurrentVersion = existingPlugin.Metadata.Version,
                    NewVersion = pluginUpdateSource.Version,
                    IcoPath = existingPlugin.Metadata.IcoPath,
                    PluginExistingMetadata = existingPlugin.Metadata,
                    PluginNewUserPlugin = pluginUpdateSource
                }).ToList();

        // No updates
        if (!resultsForUpdate.Any())
        {
            if (!silentUpdate)
            {
                API.ShowMsg(API.GetTranslation("updateNoResultTitle"), API.GetTranslation("updateNoResultSubtitle"));
            }
            return;
        }

        // If all plugins are modified, just return
        if (resultsForUpdate.All(x => API.PluginModified(x.ID)))
        {
            return;
        }

        // Show message box with button to update all plugins
        API.ShowMsgWithButton(
            API.GetTranslation("updateAllPluginsTitle"),
            API.GetTranslation("updateAllPluginsButtonContent"),
            () =>
            {
                UpdateAllPlugins(resultsForUpdate);
            },
            string.Join(", ", resultsForUpdate.Select(x => x.PluginExistingMetadata.Name)));
    }

    private static void UpdateAllPlugins(IEnumerable<PluginUpdateInfo> resultsForUpdate)
    {
        _ = Task.WhenAll(resultsForUpdate.Select(async plugin =>
        {
            var downloadToFilePath = Path.Combine(Path.GetTempPath(), $"{plugin.Name}-{plugin.NewVersion}.zip");

            try
            {
                using var cts = new CancellationTokenSource();

                await DownloadFileAsync(
                    $"{API.GetTranslation("DownloadingPlugin")} {plugin.PluginNewUserPlugin.Name}",
                    plugin.PluginNewUserPlugin.UrlDownload, downloadToFilePath, cts);

                // check if user cancelled download before installing plugin
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (!await API.UpdatePluginAsync(plugin.PluginExistingMetadata, plugin.PluginNewUserPlugin, downloadToFilePath))
                {
                    return;
                }
            }
            catch (Exception e)
            {
                API.LogException(ClassName, "Failed to update plugin", e);
                API.ShowMsgError(API.GetTranslation("ErrorUpdatingPlugin"));
            }
        }));
    }

    /// <summary>
    /// Downloads a file from a URL to a local path, optionally showing a progress box and handling cancellation.
    /// </summary>
    /// <param name="progressBoxTitle">The title for the progress box.</param>
    /// <param name="downloadUrl">The URL to download from.</param>
    /// <param name="filePath">The local file path to save to.</param>
    /// <param name="cts">Cancellation token source for cancelling the download.</param>
    /// <param name="deleteFile">Whether to delete the file if it already exists.</param>
    /// <param name="showProgress">Whether to show a progress box during download.</param>
    /// <returns>A Task representing the asynchronous download operation.</returns>
    private static async Task DownloadFileAsync(string progressBoxTitle, string downloadUrl, string filePath, CancellationTokenSource cts, bool deleteFile = true, bool showProgress = true)
    {
        if (deleteFile && File.Exists(filePath))
            File.Delete(filePath);

        if (showProgress)
        {
            var exceptionHappened = false;
            await API.ShowProgressBoxAsync(progressBoxTitle,
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

    /// <summary>
    /// Determines if the plugin install source is a known/approved source (e.g., GitHub and matches an existing plugin author).
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if the source is known, otherwise false.</returns>
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

    private record PluginUpdateInfo
    {
        public string ID { get; init; }
        public string Name { get; init; }
        public string Author { get; init; }
        public string CurrentVersion { get; init; }
        public string NewVersion { get; init; }
        public string IcoPath { get; init; }
        public PluginMetadata PluginExistingMetadata { get; init; }
        public UserPlugin PluginNewUserPlugin { get; init; }
    }
}
