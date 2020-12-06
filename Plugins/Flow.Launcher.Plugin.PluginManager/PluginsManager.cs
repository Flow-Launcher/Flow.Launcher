using Flow.Launcher.Infrastructure.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        internal void PluginInstall(Plugin plugin)
        {
            if (PluginExists())
            {
                //prompt user if want to install

                return;
            }

            PluginDowload(plugin.Url, "");
        }

        private void PluginDowload(string downloadUrl, string toFilePath)
        {
            using (var wc = new WebClient { Proxy = Http.WebProxy() })
            {
                wc.DownloadFile(downloadUrl, toFilePath);
            }
        }

        internal void PluginUpdate()
        {

        }

        internal bool PluginExists()
        {
            return false;
        }

        internal void PluginsManifestSiteOpen()
        {

        }
    }
}
