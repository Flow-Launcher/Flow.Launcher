using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Shapes;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using Path = System.IO.Path;

namespace Flow.Launcher.Core
{
    public class Updater
    {
        public string GitHubRepository { get; }

        public Updater(string gitHubRepository)
        {
            GitHubRepository = gitHubRepository;
        }

        private SemaphoreSlim UpdateLock { get; } = new SemaphoreSlim(1);

        public async Task UpdateAppAsync(IPublicAPI api, bool silentUpdate = true)
        {
            await UpdateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("pleaseWait"),
                        api.GetTranslation("update_flowlauncher_update_check"));

                var updateManager = new UpdateManager(new GithubSource(GitHubRepository, null, false));

                // UpdateApp CheckForUpdate will return value only if the app is squirrel installed
                var newUpdateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (newUpdateInfo == null)
                {
                    if (!silentUpdate)
                        api.ShowMsg(api.GetTranslation("update_flowlauncher_fail"),
                            api.GetTranslation("update_flowlauncher_check_connection"));
                    return;
                }

                var newReleaseVersion = Version.Parse(newUpdateInfo.TargetFullRelease.Version.ToString());
                var currentVersion = Version.Parse(Constant.Version);

                Log.Info($"|Updater.UpdateApp|Future Release <{newUpdateInfo.TargetFullRelease.Formatted()}>");

                if (newReleaseVersion <= currentVersion)
                {
                    if (!silentUpdate)
                        MessageBox.Show(api.GetTranslation("update_flowlauncher_already_on_latest"));
                    return;
                }

                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("update_flowlauncher_update_found"),
                        api.GetTranslation("update_flowlauncher_updating"));

                await updateManager.DownloadUpdatesAsync(newUpdateInfo).ConfigureAwait(false);

                if (DataLocation.PortableDataLocationInUse())
                {
                    var targetDestination = Path.Combine(VelopackLocator.GetDefault(null).RootAppDir!,
                        DataLocation.PortableFolderName);

                    DataLocation.PortableDataPath.CopyAll(targetDestination);
                    if (!DataLocation.PortableDataPath.VerifyBothFolderFilesEqual(targetDestination))
                        MessageBox.Show(string.Format(
                            api.GetTranslation("update_flowlauncher_fail_moving_portable_user_profile_data"),
                            DataLocation.PortableDataPath,
                            targetDestination));
                }

                var newVersionTips = NewVersionTips(newReleaseVersion.ToString());

                Log.Info($"|Updater.UpdateApp|Update success:{newVersionTips}");

                if (MessageBox.Show(newVersionTips, api.GetTranslation("update_flowlauncher_new_update"),
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    updateManager.ApplyUpdatesAndRestart(newUpdateInfo);
                }
                else
                {
                    updateManager.WaitExitThenApplyUpdates(newUpdateInfo);
                }
            }
            catch (Exception e)
            {
                if ((e is HttpRequestException or WebException or SocketException ||
                     e.InnerException is TimeoutException))
                    Log.Exception(
                        $"|Updater.UpdateApp|Check your connection and proxy settings to github-cloud.s3.amazonaws.com.",
                        e);
                else
                    Log.Exception($"|Updater.UpdateApp|Error Occurred", e);

                if (!silentUpdate)
                    api.ShowMsg(api.GetTranslation("update_flowlauncher_fail"),
                        api.GetTranslation("update_flowlauncher_check_connection"));
            }
            finally
            {
                UpdateLock.Release();
            }
        }

        public static void RecoverPortableData()
        {
            var locator = VelopackLocator.GetDefault(null);

            var portableDataLocation = Path.Combine(VelopackLocator.GetDefault(null).RootAppDir!,
                DataLocation.PortableFolderName);

            if (Path.Exists(portableDataLocation))
            {
                portableDataLocation.CopyAll(Path.Combine(locator.AppContentDir!, DataLocation.PortableFolderName));
            }

            Directory.Delete(portableDataLocation);
        }

        public string NewVersionTips(string version)
        {
            var translator = InternationalizationManager.Instance;
            var tips = string.Format(translator.GetTranslation("newVersionTips"), version);

            return tips;
        }
    }
}
