using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        private static readonly string ClassName = nameof(PluginsManifest);

        private static readonly CommunityPluginStore mainPluginStore =
            new("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/plugin_api_v2/plugins.json",
                "https://fastly.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
                "https://gcore.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
                "https://cdn.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json");

        private static readonly SemaphoreSlim manifestUpdateLock = new(1);

        private static DateTime lastFetchedAt = DateTime.MinValue;
        private static readonly TimeSpan fetchTimeout = TimeSpan.FromMinutes(2);

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

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
                    if (results.Count == 0)
                        return false;

                    lastFetchedAt = DateTime.Now;

                    var updatedPluginResult = new List<UserPlugin>();
                    var appVersion = SemanticVersioning.Version.Parse(Constant.Version);
                    
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (IsMinimumAppVersionSatisfied(results[i], appVersion))
                            updatedPluginResult.Add(results[i]);
                    }

                    UserPlugins = updatedPluginResult;

                    return true;
                }
            }
            catch (Exception e)
            {
                API.LogException(ClassName, "Http request failed", e);
            }
            finally
            {
                manifestUpdateLock.Release();
            }

            return false;
        }

        private static bool IsMinimumAppVersionSatisfied(UserPlugin plugin, SemanticVersioning.Version appVersion)
        {
            if (string.IsNullOrEmpty(plugin.MinimumAppVersion) || appVersion >= SemanticVersioning.Version.Parse(plugin.MinimumAppVersion))
                return true;

            API.LogDebug(ClassName, $"Plugin {plugin.Name} requires minimum Flow Launcher version {plugin.MinimumAppVersion}, "
                    + $"but current version is {Constant.Version}. Plugin excluded from manifest.");

            return false;
        }
    }
}
