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
using Version = SemanticVersioning.Version;

namespace Flow.Launcher.Core.Plugin;

/// <summary>
/// Class for installing, updating, and uninstalling plugins.
/// </summary>
public static class PluginInstaller
{
    private static readonly string ClassName = nameof(PluginInstaller);

    private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

    private static readonly SemaphoreSlim UpdatePluginsSemaphore = new(1, 1);

    /// <summary>
    /// Installs a plugin and restarts the application if required by settings. Prompts user for confirmation and handles download if needed.
    /// </summary>
    /// <param name="newPlugin">The plugin to install.</param>
    /// <returns>A Task representing the asynchronous install operation.</returns>
    public static async Task InstallPluginAndCheckRestartAsync(UserPlugin newPlugin)
    {
        if (PublicApi.Instance.PluginModified(newPlugin.ID))
        {
            PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(newPlugin.Name),
                Localize.pluginModifiedAlreadyMessage());
            return;
        }

        if (PublicApi.Instance.ShowMsgBox(
            Localize.InstallPromptSubtitle(newPlugin.Name, newPlugin.Author, Environment.NewLine),
            Localize.InstallPromptTitle(),
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
                    $"{Localize.DownloadingPlugin()} {newPlugin.Name}",
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

            if (!PublicApi.Instance.InstallPlugin(newPlugin, filePath))
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
            PublicApi.Instance.LogException(ClassName, "Failed to install plugin", e);
            PublicApi.Instance.ShowMsgError(Localize.ErrorInstallingPlugin());
            return; // do not restart on failure
        }

        var hotReloaded = Settings.PluginModifiedAction == PluginModifiedAction.HotReload && await PluginManager.ReloadPluginAsync(newPlugin.ID);
        if (hotReloaded)
        {
            PublicApi.Instance.ShowMsg(
                Localize.installbtn(),
                Localize.InstallSuccessHotReload(newPlugin.Name));
        }
        else if (Settings.PluginModifiedAction == PluginModifiedAction.AutoRestart)
        {
            PublicApi.Instance.RestartApp();
        }
        else if (Settings.PluginModifiedAction == PluginModifiedAction.Manual)
        {
            PublicApi.Instance.ShowMsg(
                Localize.installbtn(),
                Localize.InstallSuccessNoRestart(newPlugin.Name));
        }
        // Hot reload was attempted and failed: PluginManager.ReloadPluginAsync already notified the user
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
            PublicApi.Instance.LogException(ClassName, "Failed to validate zip file", e);
            PublicApi.Instance.ShowMsgError(Localize.ZipFileNotHavePluginJson());
            return;
        }

        if (PublicApi.Instance.PluginModified(plugin.ID))
        {
            PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(plugin.Name),
                Localize.pluginModifiedAlreadyMessage());
            return;
        }

        if (Settings.ShowUnknownSourceWarning)
        {
            if (!InstallSourceKnown(plugin.Website)
                && PublicApi.Instance.ShowMsgBox(Localize.InstallFromUnknownSourceSubtitle(Environment.NewLine),
                    Localize.InstallFromUnknownSourceTitle(),
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
        if (PublicApi.Instance.PluginModified(oldPlugin.ID))
        {
            PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(oldPlugin.Name),
                Localize.pluginModifiedAlreadyMessage());
            return;
        }

        if (PublicApi.Instance.ShowMsgBox(
            Localize.UninstallPromptSubtitle(oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
            Localize.UninstallPromptTitle(),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        var removePluginSettings = PublicApi.Instance.ShowMsgBox(
            Localize.KeepPluginSettingsSubtitle(),
            Localize.KeepPluginSettingsTitle(),
            button: MessageBoxButton.YesNo) == MessageBoxResult.No;

        try
        {
            if (!await PublicApi.Instance.UninstallPluginAsync(oldPlugin, removePluginSettings))
            {
                return;
            }
        }
        catch (Exception e)
        {
            PublicApi.Instance.LogException(ClassName, "Failed to uninstall plugin", e);
            PublicApi.Instance.ShowMsgError(Localize.ErrorUninstallingPlugin());
            return; // don not restart on failure
        }

        if (Settings.PluginModifiedAction == PluginModifiedAction.HotReload && !PublicApi.Instance.PluginModified(oldPlugin.ID))
        {
            // The plugin was fully unloaded and its directory deleted, so no restart is needed
            PublicApi.Instance.ShowMsg(
                Localize.uninstallbtn(),
                Localize.UninstallSuccessHotReload(oldPlugin.Name));
        }
        else if (Settings.PluginModifiedAction == PluginModifiedAction.AutoRestart)
        {
            PublicApi.Instance.RestartApp();
        }
        else
        {
            PublicApi.Instance.ShowMsg(
                Localize.uninstallbtn(),
                Localize.UninstallSuccessNoRestart(oldPlugin.Name));
        }
    }

    /// <summary>
    /// Updates a plugin to a new version and restarts the application if required by settings. Prompts user for confirmation and handles download if needed.
    /// </summary>
    /// <param name="newPlugin">The new plugin version to install.</param>
    /// <param name="oldPlugin">The existing plugin metadata to update.</param>
    /// <returns>True if the update was successful; otherwise false.</returns>
    public static async Task<bool> UpdatePluginAndCheckRestartAsync(UserPlugin newPlugin, PluginMetadata oldPlugin)
    {
        if (PublicApi.Instance.ShowMsgBox(
            Localize.UpdatePromptSubtitle(oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
            Localize.UpdatePromptTitle(),
            button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;

        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{newPlugin.Name}-{newPlugin.Version}.zip");

            using var cts = new CancellationTokenSource();

            if (!newPlugin.IsFromLocalInstallPath)
            {
                await DownloadFileAsync(
                    $"{Localize.DownloadingPlugin()} {newPlugin.Name}",
                    newPlugin.UrlDownload, filePath, cts);
            }
            else
            {
                filePath = newPlugin.LocalInstallPath;
            }

            // check if user cancelled download before installing plugin
            if (cts.IsCancellationRequested)
            {
                return false;
            }

            if (!await PublicApi.Instance.UpdatePluginAsync(oldPlugin, newPlugin, filePath))
            {
                return false;
            }
        }
        catch (Exception e)
        {
            PublicApi.Instance.LogException(ClassName, "Failed to update plugin", e);
            PublicApi.Instance.ShowMsgError(Localize.ErrorUpdatingPlugin());
            return false; // do not restart on failure
        }

        var hotReloaded = Settings.PluginModifiedAction == PluginModifiedAction.HotReload && await PluginManager.ReloadPluginAsync(oldPlugin.ID);
        if (hotReloaded)
        {
            PublicApi.Instance.ShowMsg(
                Localize.updatebtn(),
                Localize.UpdateSuccessHotReload(newPlugin.Name));
        }
        else if (Settings.PluginModifiedAction == PluginModifiedAction.AutoRestart)
        {
            PublicApi.Instance.RestartApp();
        }
        else if (Settings.PluginModifiedAction == PluginModifiedAction.Manual)
        {
            PublicApi.Instance.ShowMsg(
                Localize.updatebtn(),
                Localize.UpdateSuccessNoRestart(newPlugin.Name));
        }
        // Hot reload was attempted and failed: PluginManager.ReloadPluginAsync already notified the user

        return true;
    }

    /// <summary>
    /// Updates the plugin to the latest version available from its source.
    /// </summary>
    /// <param name="updateAllPlugins">Asynchronous action to execute when the user chooses to update all plugins.</param>
    /// <param name="silentUpdate">If true, do not show any messages when there is no update available.</param>
    /// <param name="usePrimaryUrlOnly">If true, only use the primary URL for updates.</param>
    /// <param name="token">Cancellation token to cancel the update operation.</param>
    /// <returns></returns>
    public static async Task CheckForPluginUpdatesAsync(Func<List<PluginUpdateInfo>, Task> updateAllPlugins, bool silentUpdate = true, bool usePrimaryUrlOnly = false, CancellationToken token = default)
    {
        // Update the plugin manifest
        await PublicApi.Instance.UpdatePluginManifestAsync(usePrimaryUrlOnly, token);

        // Get all plugins that can be updated
        var resultsForUpdate = (
            from existingPlugin in PublicApi.Instance.GetAllPlugins()
            join pluginUpdateSource in PublicApi.Instance.GetPluginManifest()
                on existingPlugin.Metadata.ID equals pluginUpdateSource.ID
            where IsUpdateAvailable(existingPlugin.Metadata.Version, pluginUpdateSource.Version)
                  && !PublicApi.Instance.PluginModified(existingPlugin.Metadata.ID)
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
        if (resultsForUpdate.Count == 0)
        {
            if (!silentUpdate)
            {
                PublicApi.Instance.ShowMsg(Localize.updateNoResultTitle(), Localize.updateNoResultSubtitle());
            }
            return;
        }

        // If all plugins are modified, just return
        if (resultsForUpdate.All(x => PublicApi.Instance.PluginModified(x.ID)))
        {
            return;
        }

        // Show message box with button to update all plugins
        PublicApi.Instance.ShowMsgWithButton(
            Localize.updateAllPluginsTitle(),
            Localize.updateAllPluginsButtonContent(),
            () =>
            {
                _ = UpdateAllPluginsFromNotificationAsync(updateAllPlugins, resultsForUpdate);
            },
            string.Join(", ", resultsForUpdate.Select(x => x.PluginExistingMetadata.Name)));
    }

    private static async Task UpdateAllPluginsFromNotificationAsync(
        Func<List<PluginUpdateInfo>, Task> updateAllPlugins,
        List<PluginUpdateInfo> resultsForUpdate)
    {
        try
        {
            await updateAllPlugins(resultsForUpdate);
        }
        catch (Exception e)
        {
            PublicApi.Instance.LogException(ClassName, "Failed to update plugins from notification", e);
        }
    }

    /// <summary>
    /// Updates all plugins that have available updates.
    /// </summary>
    /// <param name="resultsForUpdate"></param>
    /// <param name="restart"></param>
    /// <returns>The plugins that were updated successfully.</returns>
    public static async Task<IReadOnlyList<PluginUpdateInfo>> UpdateAllPluginsAsync(
        IEnumerable<PluginUpdateInfo> resultsForUpdate,
        bool restart)
    {
        await UpdatePluginsSemaphore.WaitAsync();
        try
        {
            return await UpdateAllPluginsCoreAsync(resultsForUpdate, restart);
        }
        finally
        {
            UpdatePluginsSemaphore.Release();
        }
    }

    private static async Task<IReadOnlyList<PluginUpdateInfo>> UpdateAllPluginsCoreAsync(
        IEnumerable<PluginUpdateInfo> resultsForUpdate,
        bool restart)
    {
        var allPluginsHotReloaded = true;
        // Set only for failures PluginManager.ReloadPluginAsync doesn't already notify the user about
        // (download/update failures, or hot reload being off) so the aggregate message below doesn't
        // duplicate the per-plugin notification a failed reload attempt already showed
        var anyUnnotifiedFailure = false;
        var updateResults = await Task.WhenAll(resultsForUpdate.Select(async plugin =>
        {
            var downloadToFilePath = Path.Combine(Path.GetTempPath(), $"{plugin.Name}-{plugin.NewVersion}.zip");

            try
            {
                using var cts = new CancellationTokenSource();

                await DownloadFileAsync(
                    $"{Localize.DownloadingPlugin()} {plugin.PluginNewUserPlugin.Name}",
                    plugin.PluginNewUserPlugin.UrlDownload, downloadToFilePath, cts);

                // check if user cancelled download before installing plugin
                if (cts.IsCancellationRequested)
                {
                    allPluginsHotReloaded = false;
                    anyUnnotifiedFailure = true;
                    return null;
                }

                if (!await PublicApi.Instance.UpdatePluginAsync(plugin.PluginExistingMetadata, plugin.PluginNewUserPlugin, downloadToFilePath))
                {
                    allPluginsHotReloaded = false;
                    anyUnnotifiedFailure = true;
                    return null;
                }

                if (Settings.PluginModifiedAction != PluginModifiedAction.HotReload || !await PluginManager.ReloadPluginAsync(plugin.ID))
                {
                    allPluginsHotReloaded = false;
                    if (Settings.PluginModifiedAction != PluginModifiedAction.HotReload)
                    {
                        anyUnnotifiedFailure = true;
                    }
                    // else: reload was attempted and failed, and PluginManager already notified the user
                }

                return plugin;
            }
            catch (Exception e)
            {
                allPluginsHotReloaded = false;
                anyUnnotifiedFailure = true;
                PublicApi.Instance.LogException(ClassName, "Failed to update plugin", e);
                PublicApi.Instance.ShowMsgError(Localize.ErrorUpdatingPlugin());
                return null;
            }
        }));

        var successfulUpdates = updateResults
            .OfType<PluginUpdateInfo>()
            .ToList();

        if (successfulUpdates.Count == 0) return successfulUpdates;

        if (allPluginsHotReloaded)
        {
            PublicApi.Instance.ShowMsg(
                Localize.updatebtn(),
                Localize.PluginsUpdateSuccessHotReload());
        }
        else if (restart)
        {
            PublicApi.Instance.RestartApp();
        }
        else if (anyUnnotifiedFailure)
        {
            PublicApi.Instance.ShowMsg(
                Localize.updatebtn(),
                Localize.PluginsUpdateSuccessNoRestart());
        }

        return successfulUpdates;
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
            await PublicApi.Instance.ShowProgressBoxAsync(progressBoxTitle,
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
                        await PublicApi.Instance.HttpDownloadAsync(downloadUrl, filePath, reportProgress, cts.Token).ConfigureAwait(false);
                    }
                }, cts.Cancel);

            // if exception happened while downloading and user does not cancel downloading,
            // we need to redownload the plugin
            if (exceptionHappened && (!cts.IsCancellationRequested))
                await PublicApi.Instance.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
        }
        else
        {
            await PublicApi.Instance.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
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

        return PublicApi.Instance.GetAllPlugins().Any(x =>
            !string.IsNullOrEmpty(x.Metadata.Website) &&
            x.Metadata.Website.StartsWith(constructedUrlPart)
        );
    }

    /// <summary>
    /// Determines if an update is available by comparing semantic versions, with invariant string comparison as a fallback.
    /// </summary>
    /// <param name="currentVersion">The currently installed version string.</param>
    /// <param name="latestVersion">The latest available version string from the manifest.</param>
    /// <returns>True if latestVersion is greater than currentVersion; otherwise false.</returns>
    internal static bool IsUpdateAvailable(string currentVersion, string latestVersion)
    {
        if (TryParseSemanticVersion(currentVersion, out var current) &&
            TryParseSemanticVersion(latestVersion, out var latest))
        {
            return current < latest;
        }

        // Third-party plugins may use version formats that are not valid semantic versions.
        // Preserve the previous comparison behavior so those plugins are not silently omitted.
        return string.Compare(currentVersion, latestVersion, StringComparison.InvariantCulture) < 0;
    }

    private static bool TryParseSemanticVersion(string value, out Version version)
    {
        if (Version.TryParse(value, out version))
        {
            return true;
        }

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var suffixIndex = value.IndexOfAny(new[] { '-', '+' });
        var coreLength = suffixIndex >= 0 ? suffixIndex : value.Length;
        var componentCount = value[..coreLength].Split('.').Length;

        if (componentCount is not (1 or 2))
        {
            return false;
        }

        var missingComponents = componentCount == 1 ? ".0.0" : ".0";
        var normalized = suffixIndex >= 0
            ? value.Insert(suffixIndex, missingComponents)
            : value + missingComponents;

        return Version.TryParse(normalized, out version);
    }
}

public record PluginUpdateInfo
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
