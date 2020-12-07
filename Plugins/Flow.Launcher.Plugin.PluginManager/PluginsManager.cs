using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.PluginsManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        private PluginsManifest pluginsManifest;
        private PluginInitContext context { get; set; }

        private string icoPath = "Images\\plugin.png";

        internal PluginsManager(PluginInitContext context)
        {
            pluginsManifest = new PluginsManifest();
            this.context = context;
        }
        internal void PluginInstall(UserPlugin plugin)
        {
            if (PluginExists())
            {
                //prompt user if want to install

                return;
            }

            var filePath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}{plugin.ID}.zip");
            PluginDownload(plugin.UrlDownload, filePath);
        }

        private void PluginDownload(string downloadUrl, string toFilePath)
        {
            try
            {
                using (var wc = new WebClient { Proxy = Http.WebProxy() })
                {
                    wc.DownloadFile(downloadUrl, toFilePath);
                }

                context.API.ShowMsg(context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                context.API.GetTranslation("plugin_pluginsmanager_download_success"));
            }
            catch(Exception e)
            {
                context.API.ShowMsg(context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                context.API.GetTranslation("plugin_pluginsmanager_download_success"));

                Log.Exception("PluginsManager", "An error occured while downloading plugin", e, "PluginDownload");
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

            pluginsManifest
                .UserPlugins
                    .ForEach(x => results.Add(
                        new Result
                        {
                            Title = $"{x.Name} by {x.Author}",
                            SubTitle = x.Description,
                            IcoPath = icoPath,
                            Action = e =>
                            {
                                context.API.ShowMsg(context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                    context.API.GetTranslation("plugin_pluginsmanager_please_wait"));
                                Application.Current.MainWindow.Hide();
                                PluginInstall(x);

                                return true;
                            }
                        }));

            if (string.IsNullOrEmpty(searchName))
                return results;

            return results
                    .Where(x => StringMatcher.FuzzySearch(searchName, x.Title).IsSearchPrecisionScoreMet())
                    .Select(x => x)
                    .ToList();
        }
    }
}
