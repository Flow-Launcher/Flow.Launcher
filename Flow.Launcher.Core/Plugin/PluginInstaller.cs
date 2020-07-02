using System;
using System.IO;
using System.Windows;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Flow.Launcher.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginInstaller
    {
        internal static void Install(string path)
        {
            if (File.Exists(path))
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), "flowlauncher", "plugins");
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
                UnZip(path, tempFolder, true);

                string jsonPath = Path.Combine(tempFolder, Constant.PluginMetadataFileName);
                if (!File.Exists(jsonPath))
                {
                    MessageBox.Show("Install failed: plugin config is missing");
                    return;
                }

                PluginMetadata plugin = GetMetadataFromJson(tempFolder);
                if (plugin == null || plugin.Name == null)
                {
                    MessageBox.Show("Install failed: plugin config is invalid");
                    return;
                }

                string pluginFolderPath = Infrastructure.UserSettings.DataLocation.PluginsDirectory;

                string newPluginName = plugin.Name
                    .Replace("/", "_")
                    .Replace("\\", "_")
                    .Replace(":", "_")
                    .Replace("<", "_")
                    .Replace(">", "_")
                    .Replace("?", "_")
                    .Replace("*", "_")
                    .Replace("|", "_")
                    + "-" + Guid.NewGuid();

                string newPluginPath = Path.Combine(pluginFolderPath, newPluginName);

                string content = $"Do you want to install following plugin?{Environment.NewLine}{Environment.NewLine}" +
                                 $"Name: {plugin.Name}{Environment.NewLine}" +
                                 $"Version: {plugin.Version}{Environment.NewLine}" +
                                 $"Author: {plugin.Author}";
                PluginPair existingPlugin = PluginManager.GetPluginForId(plugin.ID);

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

                    Directory.Move(tempFolder, newPluginPath);

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
                        PluginManager.API.RestartApp();
                    }
                }
            }
        }

        private static PluginMetadata GetMetadataFromJson(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, Constant.PluginMetadataFileName);
            PluginMetadata metadata;

            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginDirectory = pluginDirectory;
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginInstaller.GetMetadataFromJson|plugin config {configPath} failed: invalid json format", e);
                return null;
            }

            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                Log.Error($"|PluginInstaller.GetMetadataFromJson|plugin config {configPath} failed: invalid language {metadata.Language}");
                return null;
            }
            if (!File.Exists(metadata.ExecuteFilePath))
            {
                Log.Error($"|PluginInstaller.GetMetadataFromJson|plugin config {configPath} failed: file {metadata.ExecuteFilePath} doesn't exist");
                return null;
            }

            return metadata;
        }

        /// <summary>
        /// unzip plugin contents to the given directory.
        /// </summary>
        /// <param name="zipFile">The path to the zip file.</param>
        /// <param name="strDirectory">The output directory.</param>
        /// <param name="overWrite">overwirte</param>
        private static void UnZip(string zipFile, string strDirectory, bool overWrite)
        {
            if (strDirectory == "")
                strDirectory = Directory.GetCurrentDirectory();

            using (ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipFile)))
            {
                ZipEntry theEntry;

                while ((theEntry = zipStream.GetNextEntry()) != null)
                {
                    var pathToZip = theEntry.Name;
                    var directoryName = String.IsNullOrEmpty(pathToZip) ? "" : Path.GetDirectoryName(pathToZip);
                    var fileName = Path.GetFileName(pathToZip);
                    var destinationDir = Path.Combine(strDirectory, directoryName);
                    var destinationFile = Path.Combine(destinationDir, fileName);

                    Directory.CreateDirectory(destinationDir);

                    if (String.IsNullOrEmpty(fileName) || (File.Exists(destinationFile) && !overWrite))
                        continue;

                    using (FileStream streamWriter = File.Create(destinationFile))
                    {
                        zipStream.CopyTo(streamWriter);
                    }
                }
            }
        }
    }
}
