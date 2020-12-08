using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.PluginsManager.Models;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
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
            Application.Current.Dispatcher.Invoke(() => Install(plugin, filePath));
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

                UnZip(zipFilePath, tempPluginFolderPath, true);

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

                var existingPlugin = context.API.GetAllPlugins().Where(x => x.Metadata.ID == plugin.ID).FirstOrDefault();

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

                    //exsiting plugins may be has loaded by application,
                    //if we try to delelte those kind of plugins, we will get a  error that indicate the
                    //file is been used now.
                    //current solution is to restart Flow Launcher. Ugly.
                    //if (MainWindow.Initialized)
                    //{
                    //    Plugins.Initialize();
                    //}
                    if (MessageBox.Show($"You have installed plugin {plugin.Name} successfully.{Environment.NewLine}" +
                                        "Restart Flow Launcher to take effect?",
                                        "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        context.API.RestartApp();
                    }
                }
            }
        }

        /// <summary>
        /// unzip plugin contents to the given directory.
        /// </summary>
        /// <param name="zipFilePath">The path to the zip file.</param>
        /// <param name="strDirectory">The output directory.</param>
        /// <param name="overwrite">overwrite</param>
        private void UnZip(string zipFilePath, string strDirectory, bool overwrite)
        {
            if (strDirectory == "")
                strDirectory = Directory.GetCurrentDirectory();

            using (ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipFilePath)))
            {
                ZipEntry theEntry;

                while ((theEntry = zipStream.GetNextEntry()) != null)
                {
                    var pathToZip = theEntry.Name;
                    var directoryName = string.IsNullOrEmpty(pathToZip) ? "" : Path.GetDirectoryName(pathToZip);
                    var fileName = Path.GetFileName(pathToZip);
                    var destinationDir = Path.Combine(strDirectory, directoryName);
                    var destinationFile = Path.Combine(destinationDir, fileName);

                    Directory.CreateDirectory(destinationDir);

                    if (string.IsNullOrEmpty(fileName) || (File.Exists(destinationFile) && !overwrite))
                        continue;

                    using (FileStream streamWriter = File.Create(destinationFile))
                    {
                        zipStream.CopyTo(streamWriter);
                    }
                }
            }
        }

        //delete the zip file when done
    }
}
