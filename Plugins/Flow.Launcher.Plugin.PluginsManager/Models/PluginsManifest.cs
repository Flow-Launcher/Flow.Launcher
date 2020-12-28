using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.PluginsManager.Models
{
    internal class PluginsManifest
    {
        internal List<UserPlugin> UserPlugins { get; private set; }
        internal PluginsManifest()
        {
            Task.Run(() => DownloadManifest()).Wait();
        }

        internal async Task DownloadManifest()
        {
            var json = string.Empty;
            try
            {
                json = await Http.Get("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/main/plugins.json");

                UserPlugins = JsonConvert.DeserializeObject<List<UserPlugin>>(json);
            }
            catch (Exception e)
            {
                Log.Exception("|PluginManagement.GetManifest|Encountered error trying to download plugins manifest", e);

                UserPlugins = new List<UserPlugin>();
            }

        }
    }
}
