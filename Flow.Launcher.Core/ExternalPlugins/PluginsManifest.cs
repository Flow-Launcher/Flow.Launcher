using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        static PluginsManifest()
        {
            UpdateTask = UpdateManifestAsync();
        }

        public static List<UserPlugin> UserPlugins { get; private set; } = new List<UserPlugin>();

        public static Task UpdateTask { get; private set; }

        private static readonly SemaphoreSlim manifestUpdateLock = new(1);

        public static Task UpdateManifestAsync()
        {
            if (manifestUpdateLock.CurrentCount == 0)
            {
                return UpdateTask;
            }

            return UpdateTask = DownloadManifestAsync();
        }

        private static async Task DownloadManifestAsync()
        {
            try
            {
                await manifestUpdateLock.WaitAsync().ConfigureAwait(false);

                await using var jsonStream = await Http.GetStreamAsync("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/plugin_api_v2/plugins.json")
                                 .ConfigureAwait(false);

                UserPlugins = await JsonSerializer.DeserializeAsync<List<UserPlugin>>(jsonStream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Exception("|PluginManagement.GetManifest|Encountered error trying to download plugins manifest", e);

                UserPlugins = new List<UserPlugin>();
            }
            finally
            {
                manifestUpdateLock.Release();
            }
        }
    }
}