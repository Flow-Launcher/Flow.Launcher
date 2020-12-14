using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.PluginsManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        private readonly PluginsManifest pluginsManifest;
        private PluginInitContext Context { get; set; }

        private readonly string icoPath = "Images\\pluginsmanager.png";

        internal PluginsManager(PluginInitContext context)
        {
            pluginsManifest = new PluginsManifest();
            Context = context;
        }
        internal void InstallOrUpdate(UserPlugin plugin)
        {
            if (PluginExists(plugin.ID)) 
            {
                Context.API.ShowMsg("Plugin already installed");
                return;
            }

            var message = string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_prompt"), 
                                                                        Environment.NewLine, Environment.NewLine, 
                                                                        plugin.Name, plugin.Author);

            if(MessageBox.Show(message, Context.API.GetTranslation("plugin_pluginsmanager_install_title"), MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            var filePath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}{plugin.ID}.zip");
            
            try
            {
                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                    Context.API.GetTranslation("plugin_pluginsmanager_please_wait"));

                Http.Download(plugin.UrlDownload, filePath);

                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                    Context.API.GetTranslation("plugin_pluginsmanager_download_success"));
            }
            catch (Exception e)
            {
                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                Context.API.GetTranslation("plugin_pluginsmanager_download_success"));

                Log.Exception("PluginsManager", "An error occured while downloading plugin", e, "PluginDownload");
            }

            Application.Current.Dispatcher.Invoke(() => Install(plugin, filePath));
        }

        internal void Update()
        {
            throw new NotImplementedException();
        }

        internal bool PluginExists(string id)
        {
            return Context.API.GetAllPlugins().Any(x => x.Metadata.ID == id);
        }

        internal void PluginsManifestSiteOpen()
        {
            //Open from context menu https://git.vcmq.workers.dev/Flow-Launcher/Flow.Launcher.PluginsManifest
            throw new NotImplementedException();
        }

        internal List<Result> Search(List<Result> results, string searchName)
        {
            if (string.IsNullOrEmpty(searchName))
                return results;

            return results
                    .Where(x =>
                            {
                                var matchResult = StringMatcher.FuzzySearch(searchName, x.Title);
                                if (matchResult.IsSearchPrecisionScoreMet())
                                    x.Score = matchResult.Score;

                                return matchResult.IsSearchPrecisionScoreMet(); 
                            })
                    .ToList();
        }

        internal List<Result> RequestInstallOrUpdate(string searchName)
        {
            var results = 
                pluginsManifest
                .UserPlugins
                .Select(x =>
                    new Result
                    {
                        Title = $"{x.Name} by {x.Author}",
                        SubTitle = x.Description,
                        IcoPath = icoPath,
                        Action = e =>
                        {
                            Application.Current.MainWindow.Hide();
                            InstallOrUpdate(x);

                            return true;
                        }
                    })
                .ToList();

            return Search(results, searchName);
        }

        private void Install(UserPlugin plugin, string downloadedFilePath)
        {
            if (!File.Exists(downloadedFilePath))
                return;
            
            var tempFolderPath = Path.Combine(Path.GetTempPath(), "flowlauncher");
            var tempFolderPluginPath = Path.Combine(tempFolderPath, "plugin");
            
            if (Directory.Exists(tempFolderPath))
                Directory.Delete(tempFolderPath, true);

            Directory.CreateDirectory(tempFolderPath);

            var zipFilePath = Path.Combine(tempFolderPath, Path.GetFileName(downloadedFilePath));

            File.Move(downloadedFilePath, zipFilePath);

            Utilities.UnZip(zipFilePath, tempFolderPluginPath, true);

            var pluginFolderPath = Utilities.GetContainingFolderPathAfterUnzip(tempFolderPluginPath);

            var metadataJsonFilePath = string.Empty;
            if (File.Exists(Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName)))
                metadataJsonFilePath = Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName);

            if (string.IsNullOrEmpty(metadataJsonFilePath) || string.IsNullOrEmpty(pluginFolderPath))
            {
                MessageBox.Show(Context.API.GetTranslation("plugin_pluginsmanager_install_errormetadatafile"));
                return;
            }

            string newPluginPath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}{plugin.ID}");
            
            Directory.Move(pluginFolderPath, newPluginPath);

            if (MessageBox.Show(string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_successandrestart"), 
                                                                            plugin.Name, Environment.NewLine),
                                    Context.API.GetTranslation("plugin_pluginsmanager_install_title"), 
                                                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Context.API.RestartApp();
        }

        internal List<Result> RequestUninstall(string search)
        {
            var results= Context.API
                                .GetAllPlugins()
                                .Select(x =>
                                    new Result
                                    {
                                        Title = $"{x.Metadata.Name} by {x.Metadata.Author}",
                                        SubTitle = x.Metadata.Description,
                                        IcoPath = x.Metadata.IcoPath,
                                        Action = e =>
                                        {
                                            Application.Current.MainWindow.Hide();
                                            Uninstall(x.Metadata);

                                            return true;
                                        }
                                    })
                                .ToList();

            return Search(results, search);
        }

        private void Uninstall(PluginMetadata plugin)
        {
            string message = string.Format(Context.API.GetTranslation("plugin_pluginsmanager_uninstall_prompt"),
                                                                        Environment.NewLine, Environment.NewLine,
                                                                        plugin.Name, plugin.Author);

            if (MessageBox.Show(message, Context.API.GetTranslation("plugin_pluginsmanager_uninstall_title"), 
                                                                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var _ = File.CreateText(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt"));

                if (MessageBox.Show(string.Format(Context.API.GetTranslation("plugin_pluginsmanager_uninstall_successandrestart"),
                                                                            plugin.Name, Environment.NewLine),
                                    Context.API.GetTranslation("plugin_pluginsmanager_uninstall_title"),
                                                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Context.API.RestartApp();
            }
        }
    }
}
