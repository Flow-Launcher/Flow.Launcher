using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;
using System.Text.Json;

namespace Flow.Launcher.Core.Plugin
{

    internal abstract class PluginConfig
    {
        /// <summary>
        /// Parse plugin metadata in the given directories
        /// </summary>
        /// <param name="pluginDirectories"></param>
        /// <returns></returns>
        public static List<PluginMetadata> Parse(string[] pluginDirectories)
        {
            var allPluginMetadata = new List<PluginMetadata>();
            var directories = pluginDirectories.SelectMany(Directory.EnumerateDirectories);

            // todo use linq when diable plugin is implmented since parallel.foreach + list is not thread saft
            foreach (var directory in directories)
            {
                if (File.Exists(Path.Combine(directory, "NeedDelete.txt")))
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception e)
                    {
                        Log.Exception(nameof(PluginConfig),$"Can't delete <{directory}>", e);
                    }
                }
                else
                {
                    PluginMetadata metadata = GetPluginMetadata(directory);
                    if (metadata != null)
                    {
                        allPluginMetadata.Add(metadata);
                    }
                }
            }
 
            return allPluginMetadata;
        }

        private static PluginMetadata GetPluginMetadata(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, Constant.PluginMetadataFileName);
            if (!File.Exists(configPath))
            {
                Log.Error(nameof(PluginConfig),$"Didn't find config file <{configPath}>");
                return null;
            }

            PluginMetadata metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginDirectory = pluginDirectory;
                // for plugins which doesn't has ActionKeywords key
                metadata.ActionKeywords = metadata.ActionKeywords ?? new List<string> { metadata.ActionKeyword };
                // for plugin still use old ActionKeyword
                metadata.ActionKeyword = metadata.ActionKeywords?[0];
            }
            catch (Exception e)
            {
                Log.Exception(nameof(PluginConfig),$"invalid json for config <{configPath}>", e);
                return null;
            }

            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                Log.Error(nameof(PluginConfig),$"Invalid language <{metadata.Language}> for config <{configPath}>");
                return null;
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                Log.Error(nameof(PluginConfig),$"execute file path didn't exist <{metadata.ExecuteFilePath}> for conifg <{configPath}");
                return null;
            }

            return metadata;
        }
    }
}