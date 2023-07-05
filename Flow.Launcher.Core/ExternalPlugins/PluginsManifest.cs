using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        private static readonly CommunityPluginStore mainPluginStore =
            new("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/plugin_api_v2/plugins.json",
                "https://fastly.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
                "https://gcore.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
                "https://cdn.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json");

        private static readonly SemaphoreSlim manifestUpdateLock = new(1);

        private static DateTime lastFetchedAt = DateTime.MinValue;
        private static TimeSpan fetchTimeout = TimeSpan.FromSeconds(10);

        public static List<UserPlugin> UserPlugins { get; private set; }

        public static async Task UpdateManifestAsync(CancellationToken token = default)
        {
            try
            {
                await manifestUpdateLock.WaitAsync(token).ConfigureAwait(false);

                if (UserPlugins == null || DateTime.Now.Subtract(lastFetchedAt) >= fetchTimeout)
                {
                    var results = await mainPluginStore.FetchAsync(token).ConfigureAwait(false);

                    UserPlugins = results;
                    lastFetchedAt = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginsManifest.{nameof(UpdateManifestAsync)}|Http request failed", e);
            }
            finally
            {
                manifestUpdateLock.Release();
            }
        }
    }
}
