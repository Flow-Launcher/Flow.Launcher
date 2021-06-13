using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.PluginsManager.Models
{
    internal class PluginsManifest
    {
        internal List<UserPlugin> UserPlugins { get; private set; } = new List<UserPlugin>();

        internal async Task DownloadManifest()
        {
            try
            {
                await using var jsonStream = await Http
                    .GetStreamAsync(
                        "https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/main/plugins.json")
                    .ConfigureAwait(false);

                UserPlugins = await JsonSerializer.DeserializeAsync<List<UserPlugin>>(jsonStream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Exception("Flow.Plugin.PluginsManager." + nameof(PluginsManifest),
                    "Encountered error trying to download plugins manifest", e);

                UserPlugins = new List<UserPlugin>();
            }
        }
    }
}