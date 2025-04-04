﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;

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
        private static readonly TimeSpan fetchTimeout = TimeSpan.FromMinutes(2);

        public static List<UserPlugin> UserPlugins { get; private set; }

        public static async Task<bool> UpdateManifestAsync(bool usePrimaryUrlOnly = false, CancellationToken token = default)
        {
            try
            {
                await manifestUpdateLock.WaitAsync(token).ConfigureAwait(false);

                if (UserPlugins == null || usePrimaryUrlOnly || DateTime.Now.Subtract(lastFetchedAt) >= fetchTimeout)
                {
                    var results = await mainPluginStore.FetchAsync(token, usePrimaryUrlOnly).ConfigureAwait(false);

                    // If the results are empty, we shouldn't update the manifest because the results are invalid.
                    if (results.Count != 0)
                    {
                        UserPlugins = results;
                        lastFetchedAt = DateTime.Now;

                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Ioc.Default.GetRequiredService<IPublicAPI>().LogException(nameof(PluginsManifest), "Http request failed", e);
            }
            finally
            {
                manifestUpdateLock.Release();
            }

            return false;
        }
    }
}
