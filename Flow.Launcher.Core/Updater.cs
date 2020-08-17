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
using Newtonsoft.Json;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System.IO;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core
{
    public class Updater
    {
        public string GitHubRepository { get; }

        public Updater(string gitHubRepository)
        {
            GitHubRepository = gitHubRepository;
        }

        public async Task UpdateApp(IPublicAPI api , bool silentUpdate = true)
        {
            UpdateManager updateManager;
            UpdateInfo newUpdateInfo;

            if (!silentUpdate)
                api.ShowMsg("Please wait...", "Checking for new update");

            try
            {
                updateManager = await GitHubUpdateManager(GitHubRepository);
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
            {
                Log.Exception($"|Updater.UpdateApp|Please check your connection and proxy settings to api.github.com.", e);
                return;
            }

            try
            {
                // UpdateApp CheckForUpdate will return value only if the app is squirrel installed
                newUpdateInfo = await updateManager.CheckForUpdate().NonNull();
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
            {
                Log.Exception($"|Updater.UpdateApp|Check your connection and proxy settings to api.github.com.", e);
                updateManager.Dispose();
                return;
            }

            var newReleaseVersion = Version.Parse(newUpdateInfo.FutureReleaseEntry.Version.ToString());
            var currentVersion = Version.Parse(Constant.Version);

            Log.Info($"|Updater.UpdateApp|Future Release <{newUpdateInfo.FutureReleaseEntry.Formatted()}>");

            if (newReleaseVersion <= currentVersion)
            {
                if (!silentUpdate)
                    MessageBox.Show("You already have the latest Flow Launcher version");
                updateManager.Dispose();
                return;
            }

            if (!silentUpdate)
                api.ShowMsg("Update found", "Updating...");

            try
            {
                await updateManager.DownloadReleases(newUpdateInfo.ReleasesToApply);
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
            {
                Log.Exception($"|Updater.UpdateApp|Check your connection and proxy settings to github-cloud.s3.amazonaws.com.", e);
                updateManager.Dispose();
                return;
            }
            
            await updateManager.ApplyReleases(newUpdateInfo);

            if (DataLocation.PortableDataLocationInUse())
            {
                var targetDestination = updateManager.RootAppDirectory + $"\\app-{newReleaseVersion.ToString()}\\{DataLocation.PortableFolderName}";
                FilesFolders.Copy(DataLocation.PortableDataPath, targetDestination);
                if (!FilesFolders.VerifyBothFolderFilesEqual(DataLocation.PortableDataPath, targetDestination))
                    MessageBox.Show("Flow Launcher was not able to move your user profile data to the new update version. Please manually " +
                        $"move your profile data folder from {DataLocation.PortableDataPath} to {targetDestination}");
            }
            else
            {
                await updateManager.CreateUninstallerRegistryEntry();
            }

            var newVersionTips = NewVersinoTips(newReleaseVersion.ToString());
            
            Log.Info($"|Updater.UpdateApp|Update success:{newVersionTips}");

            // always dispose UpdateManager
            updateManager.Dispose();

            if (MessageBox.Show(newVersionTips, "New Update", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
        }

        [UsedImplicitly]
        private class GithubRelease
        {
            [JsonProperty("prerelease")]
            public bool Prerelease { get; [UsedImplicitly] set; }

            [JsonProperty("published_at")]
            public DateTime PublishedAt { get; [UsedImplicitly] set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; [UsedImplicitly] set; }
        }

        /// https://github.com/Squirrel/Squirrel.Windows/blob/master/src/Squirrel/UpdateManager.Factory.cs
        private async Task<UpdateManager> GitHubUpdateManager(string repository)
        {
            var uri = new Uri(repository);
            var api = $"https://api.github.com/repos{uri.AbsolutePath}/releases";

            var json = await Http.Get(api);

            var releases = JsonConvert.DeserializeObject<List<GithubRelease>>(json);
            var latest = releases.Where(r => !r.Prerelease).OrderByDescending(r => r.PublishedAt).First();
            var latestUrl = latest.HtmlUrl.Replace("/tag/", "/download/");

            var client = new WebClient { Proxy = Http.WebProxy() };
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