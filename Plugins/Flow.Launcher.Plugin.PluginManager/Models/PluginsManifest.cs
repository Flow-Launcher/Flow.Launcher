using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager.Models
{
    class PluginsManifest
    {
        public List<Plugin> Plugins { get; private set; }
        public PluginsManifest()
        {
            var json = string.Empty;

            using (var wc = new WebClient { Proxy = Http.WebProxy() })
            {
                try
                {
                    json = wc.DownloadString("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/main/plugins.json");
                }
                catch (Exception e)
                {
                    Log.Exception("|PluginManagement.GetManifest|Encountered error trying to download plugins manifest", e);

                    Plugins = new List<Plugin>();
                }
            }

            Plugins = !string.IsNullOrEmpty(json) ? JsonConvert.DeserializeObject<List<Plugin>>(json) : new List<Plugin>();
        }
    }
}
