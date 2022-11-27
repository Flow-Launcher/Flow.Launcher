using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
using Squirrel;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Text.Json.Serialization;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Core
{
    public class Updater
    {
        public string GitHubRepository { get; }

        private string updatePythonIndicatorFilename = ".updatePythonPath";

        private string updateNodeIndicatorFilename = ".updateNodePath";

        private string appDataRegex = @"app-\d\.\d\.\d";

        public Updater(string gitHubRepository)
        {
            GitHubRepository = gitHubRepository;
        }

        private SemaphoreSlim UpdateLock { get; } = new SemaphoreSlim(1);

        public async Task UpdateAppAsync(IPublicAPI api, Settings settings, bool silentUpdate = true)
        {
            await UpdateLock.WaitAsync();
            try
            {
                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("pleaseWait"),
                        api.GetTranslation("update_flowlauncher_update_check"));

                using var updateManager = await GitHubUpdateManagerAsync(GitHubRepository).ConfigureAwait(false);

                // UpdateApp CheckForUpdate will return value only if the app is squirrel installed
                var newUpdateInfo = await updateManager.CheckForUpdate().NonNull().ConfigureAwait(false);

                var newReleaseVersion = Version.Parse(newUpdateInfo.FutureReleaseEntry.Version.ToString());
                var currentVersion = Version.Parse(Constant.Version);

                Log.Info($"|Updater.UpdateApp|Future Release <{newUpdateInfo.FutureReleaseEntry.Formatted()}>");

                if (newReleaseVersion <= currentVersion)
                {
                    if (!silentUpdate)
                        MessageBox.Show(api.GetTranslation("update_flowlauncher_already_on_latest"));
                    return;
                }

                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("update_flowlauncher_update_found"),
                        api.GetTranslation("update_flowlauncher_updating"));

                await updateManager.DownloadReleases(newUpdateInfo.ReleasesToApply).ConfigureAwait(false);

                await updateManager.ApplyReleases(newUpdateInfo).ConfigureAwait(false);

                if (DataLocation.PortableDataLocationInUse())
                {
                    var targetDestination = updateManager.RootAppDirectory + $"\\app-{newReleaseVersion.ToString()}\\{DataLocation.PortableFolderName}";
                    FilesFolders.CopyAll(DataLocation.PortableDataPath, targetDestination);
                    if (!FilesFolders.VerifyBothFolderFilesEqual(DataLocation.PortableDataPath, targetDestination))
                        MessageBox.Show(string.Format(api.GetTranslation("update_flowlauncher_fail_moving_portable_user_profile_data"),
                            DataLocation.PortableDataPath,
                            targetDestination));

                    UpdatePluginEnvPaths(settings, newReleaseVersion.ToString());
                }
                else
                {
                    await updateManager.CreateUninstallerRegistryEntry().ConfigureAwait(false);
                }

                var newVersionTips = NewVersionTips(newReleaseVersion.ToString());

                Log.Info($"|Updater.UpdateApp|Update success:{newVersionTips}");

                if (MessageBox.Show(newVersionTips, api.GetTranslation("update_flowlauncher_new_update"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    UpdateManager.RestartApp(Constant.ApplicationFileName);
                }
            }
            catch (Exception e) when (e is HttpRequestException or WebException or SocketException || e.InnerException is TimeoutException)
            {
                Log.Exception($"|Updater.UpdateApp|Check your connection and proxy settings to github-cloud.s3.amazonaws.com.", e);
                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("update_flowlauncher_fail"),
                        api.GetTranslation("update_flowlauncher_check_connection"));
            }
            finally
            {
                UpdateLock.Release();
            }
        }

        [UsedImplicitly]
        private class GithubRelease
        {
            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; [UsedImplicitly] set; }

            [JsonPropertyName("published_at")]
            public DateTime PublishedAt { get; [UsedImplicitly] set; }

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; [UsedImplicitly] set; }
        }

        /// https://github.com/Squirrel/Squirrel.Windows/blob/master/src/Squirrel/UpdateManager.Factory.cs
        private async Task<UpdateManager> GitHubUpdateManagerAsync(string repository)
        {
            var uri = new Uri(repository);
            var api = $"https://api.github.com/repos{uri.AbsolutePath}/releases";

            await using var jsonStream = await Http.GetStreamAsync(api).ConfigureAwait(false);

            var releases = await System.Text.Json.JsonSerializer.DeserializeAsync<List<GithubRelease>>(jsonStream).ConfigureAwait(false);
            var latest = releases.Where(r => !r.Prerelease).OrderByDescending(r => r.PublishedAt).First();
            var latestUrl = latest.HtmlUrl.Replace("/tag/", "/download/");

            var client = new WebClient
            {
                Proxy = Http.WebProxy
            };
            var downloader = new FileDownloader(client);

            var manager = new UpdateManager(latestUrl, urlDownloader: downloader);

            return manager;
        }

        public string NewVersionTips(string version)
        {
            var translator = InternationalizationManager.Instance;
            var tips = string.Format(translator.GetTranslation("newVersionTips"), version);

            return tips;
        }

        private void UpdatePluginEnvPaths(Settings settings, string newVer)
        {
            var appVer = $"app-{newVer}";
            var updatePythonIndicatorFilePath 
                = Regex.Replace(Path.Combine(DataLocation.PluginEnvironments, updatePythonIndicatorFilename), appDataRegex, appVer);
            var updateNodeIndicatorFilePath 
                = Regex.Replace(Path.Combine(DataLocation.PluginEnvironments, updateNodeIndicatorFilename), appDataRegex, appVer);

            if (!string.IsNullOrEmpty(settings.PluginSettings.PythonExecutablePath)
                && settings.PluginSettings.PythonExecutablePath.StartsWith(DataLocation.PluginEnvironments))
                using (var _ = File.CreateText(updatePythonIndicatorFilePath)){}

            if (!string.IsNullOrEmpty(settings.PluginSettings.NodeExecutablePath)
                && settings.PluginSettings.NodeExecutablePath.StartsWith(DataLocation.PluginEnvironments))
                using (var _ = File.CreateText(updateNodeIndicatorFilePath)) { }
        }

        public void PreStartSetupAfterAppUpdate(Settings settings)
        {
            var appVer = $"app-{Constant.Version}";
            var updatePythonIndicatorFilePath = Path.Combine(DataLocation.PluginEnvironments, updatePythonIndicatorFilename);
            var updateNodeIndicatorFilePath = Path.Combine(DataLocation.PluginEnvironments, updateNodeIndicatorFilename);

            if (File.Exists(updatePythonIndicatorFilePath))
            {
                settings.PluginSettings.PythonExecutablePath
                    = Regex.Replace(settings.PluginSettings.PythonExecutablePath, appDataRegex, appVer);

                File.Delete(updatePythonIndicatorFilePath);
            }

            if (File.Exists(updateNodeIndicatorFilePath))
            {
                settings.PluginSettings.NodeExecutablePath
                    = Regex.Replace(settings.PluginSettings.NodeExecutablePath, appDataRegex, appVer);

                File.Delete(updateNodeIndicatorFilePath);
            }
        }
    }
}
