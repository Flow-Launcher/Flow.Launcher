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
using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        private readonly PluginsManifest pluginsManifest;
        private PluginInitContext Context { get; set; }

        private readonly string icoPath = "Images\\plugin.png";

        internal PluginsManager(PluginInitContext context)
        {
            pluginsManifest = new PluginsManifest();
            Context = context;
        }
        internal void InstallOrUpdate(UserPlugin plugin)
        {
            if (PluginExists())
            {
                //prompt user if want to install

                return;
            }

            var filePath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}{plugin.ID}.zip");
            
            try
            {
                Utilities.Download(plugin.UrlDownload, filePath);

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

        }

        internal bool PluginExists()
        {
            return false;
        }

        internal void PluginsManifestSiteOpen()
        {
            //Open https://git.vcmq.workers.dev/Flow-Launcher/Flow.Launcher.PluginsManifest
        }

        internal List<Result> Search(List<Result> results, string searchName)
        {
            if (string.IsNullOrEmpty(searchName))
                return results;

            return results
                    .Where(x => StringMatcher.FuzzySearch(searchName, x.Title).IsSearchPrecisionScoreMet())
                    .Select(x => x)
                    .ToList();
        }

        internal List<Result> RequestInstallOrUpdate(string searchName)
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
                                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                    Context.API.GetTranslation("plugin_pluginsmanager_please_wait"));
                                Application.Current.MainWindow.Hide();
                                InstallOrUpdate(x);

                                return true;
                            }
                        }));

            return Search(results, searchName);
        }

        private void Install(UserPlugin plugin, string downloadedFilePath)
        {
            if (File.Exists(downloadedFilePath))
            {
                var tempFolderPath = Path.Combine(Path.GetTempPath(), "flowlauncher");
                var tempPluginFolderPath = Path.Combine(tempFolderPath, "plugin");
                
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }

                Directory.CreateDirectory(tempFolderPath);

                var zipFilePath = Path.Combine(tempFolderPath, Path.GetFileName(downloadedFilePath));

                File.Move(downloadedFilePath, zipFilePath);

                Utilities.UnZip(zipFilePath, tempPluginFolderPath, true);

                var unzippedParentFolderPath = tempPluginFolderPath;

                var metadataJsonFilePath = string.Empty;

                var pluginFolderPath = string.Empty;

                var unzippedFolderCount = Directory.GetDirectories(unzippedParentFolderPath).Length;
                var unzippedFilesCount = Directory.GetFiles(unzippedParentFolderPath).Length;

                // addjust path depending on how the plugin is zipped up
                // the recommended should be to zip up the folder not the contents
                if (unzippedFolderCount == 1 && unzippedFilesCount == 0)
                    // folder is zipped up, unzipped plugin directory structure: tempPath/unzippedParentPluginFolder/pluginFolderName/
                    pluginFolderPath = Directory.GetDirectories(unzippedParentFolderPath)[0];

                if (unzippedFilesCount > 1)
                    // content is zipped up, unzipped plugin directory structure: tempPath/unzippedParentPluginFolder/
                    pluginFolderPath = unzippedParentFolderPath;

                if (File.Exists(Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName)))
                    metadataJsonFilePath = Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName);

                if (string.IsNullOrEmpty(metadataJsonFilePath) || string.IsNullOrEmpty(pluginFolderPath))
                {
                    MessageBox.Show("Install failed: unable to find the plugin.json metadata file");
                    return;
                }

                string newPluginPath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}{plugin.ID}");

                string content = $"Do you want to install following plugin?{Environment.NewLine}{Environment.NewLine}" +
                                 $"Name: {plugin.Name}{Environment.NewLine}" +
                                 $"Version: {plugin.Version}{Environment.NewLine}" +
                                 $"Author: {plugin.Author}";

                var existingPlugin = Context.API.GetAllPlugins().Where(x => x.Metadata.ID == plugin.ID).FirstOrDefault();

                if (existingPlugin != null)
                {
                    content = $"Do you want to update following plugin?{Environment.NewLine}{Environment.NewLine}" +
                              $"Name: {plugin.Name}{Environment.NewLine}" +
                              $"Old Version: {existingPlugin.Metadata.Version}" +
                              $"{Environment.NewLine}New Version: {plugin.Version}" +
                              $"{Environment.NewLine}Author: {plugin.Author}";
                }

                var result = MessageBox.Show(content, "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (existingPlugin != null && Directory.Exists(existingPlugin.Metadata.PluginDirectory))
                    {
                        //when plugin is in use, we can't delete them. That's why we need to make plugin folder a random name
                        File.Create(Path.Combine(existingPlugin.Metadata.PluginDirectory, "NeedDelete.txt")).Close();
                    }

                    Directory.Move(pluginFolderPath, newPluginPath);

                    if (MessageBox.Show($"You have installed plugin {plugin.Name} successfully.{Environment.NewLine}" +
                                        "Restart Flow Launcher to take effect?",
                                        "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        Context.API.RestartApp();
                    }
                }
            }
        }

        internal List<Result> RequestUninstall(string search)
        {
            var results = new List<Result>();

            Context.API.GetAllPlugins()
                .ForEach(x => results.Add(
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
                    }));

            return Search(results, search);
        }

        private void Uninstall(PluginMetadata plugin)
        {
            string message = Context.API.GetTranslation("plugin_pluginsmanager_uninstall_prompt")+
                                            $"{Environment.NewLine}{Environment.NewLine}" +
                                            $"{plugin.Name} by {plugin.Author}";

            if (MessageBox.Show(message, "Flow Launcher", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                File.Create(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt")).Close();
                var result = MessageBox.Show($"You have uninstalled plugin {plugin.Name} successfully.{Environment.NewLine}" +
                                             "Restart Flow Launcher to take effect?",
                                             "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Context.API.RestartApp();
                }
            }
        }
    }
}
