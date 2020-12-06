using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Plugin.PluginsManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        private PluginsManifest pluginsManifest;

        private string icoPath = "Images\\plugin.png";

        internal PluginsManager()
        {
            pluginsManifest = new PluginsManifest();
        }
        internal void PluginInstall(UserPlugin plugin)
        {
            if (PluginExists())
            {
                //prompt user if want to install

                return;
            }

            PluginDowload(plugin.UrlDownload, "");
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

        internal List<Result> PluginsSearch(string searchName)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(searchName))
            {
                pluginsManifest.UserPlugins
                    .ForEach(x => results.Add(
                        new Result
                        {
                            Title = $"{x.Name} by {x.Author}",
                            SubTitle = x.Description,
                            IcoPath = icoPath,
                            Action = e =>
                            {
                                PluginInstall(x);
                                return false;
                            }
                        }));

                return results;
            }

            pluginsManifest.UserPlugins
                .Where(x => StringMatcher.FuzzySearch(searchName, x.Name).IsSearchPrecisionScoreMet())
                .Select(x => x)
                .ToList()
                .ForEach(x => results.Add(
                    new Result
                    {
                        Title = $"{x.Name} by {x.Author}",
                        SubTitle = x.Description,
                        IcoPath = icoPath,
                        Action = e =>
                        {
                            PluginInstall(x);
                            return false;
                        }
                    }));

            return results;
        }
    }
}
