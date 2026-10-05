using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Flow.Launcher.Core.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        private static readonly string ClassName = nameof(PluginsManifest);

        private static readonly CommunityPluginStore mainPluginStore =
            new("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/main/plugins.json",
                "https://fastly.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@main/plugins.json",
                "https://gcore.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@main/plugins.json",
                "https://cdn.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@main/plugins.json");

        private static readonly SemaphoreSlim manifestUpdateLock = new(1);

        private static DateTime lastFetchedAt = DateTime.MinValue;
        private static readonly TimeSpan fetchTimeout = TimeSpan.FromMinutes(2);

        public static List<UserPlugin> UserPlugins { get; private set; }

        public static async Task<bool> UpdateManifestAsync(bool usePrimaryUrlOnly = false, CancellationToken token = default)
        {
            bool lockAcquired = false;
            try
            {
                await manifestUpdateLock.WaitAsync(token).ConfigureAwait(false);
                lockAcquired = true;

                if (UserPlugins == null || usePrimaryUrlOnly || DateTime.Now.Subtract(lastFetchedAt) >= fetchTimeout)
                {
                    var results = await mainPluginStore.FetchAsync(token, usePrimaryUrlOnly).ConfigureAwait(false);

                    // If the results are empty, we shouldn't update the manifest because the results are invalid.
                    if (results.Count == 0)
                        return false;

                    var updatedPluginResults = new List<UserPlugin>();
                    
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (PluginManager.IsMinimumAppVersionSatisfied(results[i].Name, results[i].MinimumAppVersion))
                            updatedPluginResults.Add(results[i]);
                    }

                    UserPlugins = updatedPluginResults;

                    lastFetchedAt = DateTime.Now;

                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, "Http request failed", e);
            }
            finally
            {
                // Only release the lock if it was acquired
                if (lockAcquired) manifestUpdateLock.Release();
            }

            return false;
        }
    }
}
