using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using System.Text.Json;

namespace Flow.Launcher.Core.Plugin
{
    internal abstract class PluginConfig
    {
        private static readonly string ClassName = nameof(PluginConfig);

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
                        PublicApi.Instance.LogException(ClassName, $"Can't delete <{directory}>", e);
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

            (List<PluginMetadata> uniqueList, List<PluginMetadata> duplicateList) = GetUniqueLatestPluginMetadata(allPluginMetadata);

            duplicateList
                .ForEach(
                    x => PublicApi.Instance.LogWarn(ClassName, 
                        string.Format("Duplicate plugin name: {0}, id: {1}, version: {2} " +
                            "not loaded due to version not the highest of the duplicates",
                            x.Name, x.ID, x.Version),
                            "GetUniqueLatestPluginMetadata"));

            return uniqueList;
        }

        internal static (List<PluginMetadata>, List<PluginMetadata>) GetUniqueLatestPluginMetadata(List<PluginMetadata> allPluginMetadata)
        {
            var duplicate_list = new List<PluginMetadata>();
            var unique_list = new List<PluginMetadata>();

            var duplicateGroups = allPluginMetadata.GroupBy(x => x.ID).Where(g => g.Count() > 1).Select(y => y).ToList();

            foreach (var metadata in allPluginMetadata)
            {
                var duplicatesExist = false;
                foreach (var group in duplicateGroups)
                {
                    if (metadata.ID == group.Key)
                    {
                        duplicatesExist = true;

                        // If metadata's version greater than each duplicate's version, CompareTo > 0
                        var count = group.Where(x => metadata.Version.CompareTo(x.Version) > 0).Count();
                        
                        // Only add if the meatadata's version is the highest of all duplicates in the group
                        if (count == group.Count() - 1)
                        {
                            unique_list.Add(metadata);
                        }
                        else
                        {
                            duplicate_list.Add(metadata);
                        }
                    }
                }
                
                if (!duplicatesExist)
                    unique_list.Add(metadata);
            }

            return (unique_list, duplicate_list);
        }

        private static PluginMetadata GetPluginMetadata(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, Constant.PluginMetadataFileName);
            if (!File.Exists(configPath))
            {
                PublicApi.Instance.LogError(ClassName, $"Didn't find config file <{configPath}>");
                return null;
            }

            PluginMetadata metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginDirectory = pluginDirectory;
                // for plugins which doesn't has ActionKeywords key
                metadata.ActionKeywords ??= new List<string> { metadata.ActionKeyword };
                // for plugin still use old ActionKeyword
                metadata.ActionKeyword = metadata.ActionKeywords?[0];
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Invalid json for config <{configPath}>", e);
                return null;
            }

            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                PublicApi.Instance.LogError(ClassName, $"Invalid language <{metadata.Language}> for config <{configPath}>");
                return null;
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                PublicApi.Instance.LogError(ClassName, $"Execute file path didn't exist <{metadata.ExecuteFilePath}> for conifg <{configPath}");
                return null;
            }

            return metadata;
        }
    }
}
