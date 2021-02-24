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

namespace Flow.Launcher.Core
{
    public class Updater
    {
        public string GitHubRepository { get; }

        public Updater(string gitHubRepository)
        {
            GitHubRepository = gitHubRepository;
        }

        public async Task UpdateApp(IPublicAPI api, bool silentUpdate = true)
        {
            try
            {
                UpdateInfo newUpdateInfo;

                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("pleaseWait"),
                                api.GetTranslation("update_flowlauncher_update_check"));

                using var updateManager = await GitHubUpdateManager(GitHubRepository).ConfigureAwait(false);


                // UpdateApp CheckForUpdate will return value only if the app is squirrel installed
                newUpdateInfo = await updateManager.CheckForUpdate().NonNull().ConfigureAwait(false);

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
                }
                else
                {
                    await updateManager.CreateUninstallerRegistryEntry().ConfigureAwait(false);
                }

                var newVersionTips = NewVersinoTips(newReleaseVersion.ToString());

                Log.Info($"|Updater.UpdateApp|Update success:{newVersionTips}");

                if (MessageBox.Show(newVersionTips, api.GetTranslation("update_flowlauncher_new_update"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    UpdateManager.RestartApp(Constant.ApplicationFileName);
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException || e.InnerException is TimeoutException)
            {
                Log.Exception($"|Updater.UpdateApp|Check your connection and proxy settings to github-cloud.s3.amazonaws.com.", e);
                api.ShowMsg(api.GetTranslation("update_flowlauncher_fail"),
                            api.GetTranslation("update_flowlauncher_check_connection"));
                return;
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
        private async Task<UpdateManager> GitHubUpdateManager(string repository)
        {
            var uri = new Uri(repository);
            var api = $"https://api.github.com/repos{uri.AbsolutePath}/releases";

            var jsonStream = await Http.GetStreamAsync(api).ConfigureAwait(false);

            var releases = await System.Text.Json.JsonSerializer.DeserializeAsync<List<GithubRelease>>(jsonStream).ConfigureAwait(false);
            var latest = releases.Where(r => !r.Prerelease).OrderByDescending(r => r.PublishedAt).First();
            var latestUrl = latest.HtmlUrl.Replace("/tag/", "/download/");

            var client = new WebClient { Proxy = Http.WebProxy };
            var downloader = new FileDownloader(client);

            var manager = new UpdateManager(latestUrl, urlDownloader: downloader);

            return manager;
        }

        public string NewVersinoTips(string version)
        {
            var translater = InternationalizationManager.Instance;
            var tips = string.Format(translater.GetTranslation("newVersionTips"), version);
            return tips;
        }

    }
}