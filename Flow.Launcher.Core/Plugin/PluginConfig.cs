using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using System.Text.Json;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Version = SemanticVersioning.Version;

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
                if (File.Exists(Path.Combine(directory, DataLocation.PluginDeleteFile)))
                {
                    try
                    {
                        var fullyDeleted = FilesFolders.TryDeleteDirectoryRobust(directory, maxRetries: 3, retryDelayMs: 200);
                        if (!fullyDeleted)
                        {
                            PublicApi.Instance.LogWarn(ClassName, $"Directory <{directory}> was not fully deleted.");

                            // Directory was not fully deleted, recreate the marker file so deletion will be retried on next startup
                            var markerFilePath = Path.Combine(directory, DataLocation.PluginDeleteFile);
                            if (!File.Exists(markerFilePath))
                            {
                                File.WriteAllText(markerFilePath, string.Empty);
                            }
                        }
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

            foreach (var group in duplicateGroups)
            {
                // Use a single consistent comparison strategy for the entire group
                // to avoid cycles when mixing semantic and non-semantic versions.
                var allSemantic = group.All(x => TryParseSemanticVersion(x.Version, out _));

                IOrderedEnumerable<PluginMetadata> sorted;
                if (allSemantic)
                {
                    sorted = group.OrderByDescending(x =>
                    {
                        TryParseSemanticVersion(x.Version, out var v);
                        return v;
                    });
                }
                else
                {
                    sorted = group.OrderByDescending(x => x.Version, StringComparer.InvariantCulture);
                }

                var ordered = sorted.ToList();

                // If the top two versions are tied, no single copy is uniquely highest,
                // so treat all as duplicates (preserves original behavior).
                var isTie = ordered.Count >= 2 && ordered[0].Version == ordered[1].Version;

                if (!isTie)
                {
                    unique_list.Add(ordered[0]);
                    duplicate_list.AddRange(ordered.Skip(1));
                }
                else
                {
                    duplicate_list.AddRange(ordered);
                }
            }

            // Add plugins that have no duplicates
            foreach (var metadata in allPluginMetadata)
            {
                if (!duplicateGroups.Any(g => g.Key == metadata.ID))
                {
                    unique_list.Add(metadata);
                }
            }

            return (unique_list, duplicate_list);
        }

        private static bool TryParseSemanticVersion(string value, out Version version)
        {
            if (Version.TryParse(value, out version))
            {
                return true;
            }

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var suffixIndex = value.IndexOfAny(new[] { '-', '+' });
            var coreLength = suffixIndex >= 0 ? suffixIndex : value.Length;
            var componentCount = value[..coreLength].Split('.').Length;

            if (componentCount is not (1 or 2))
            {
                return false;
            }

            var missingComponents = componentCount == 1 ? ".0.0" : ".0";
            var normalized = suffixIndex >= 0
                ? value.Insert(suffixIndex, missingComponents)
                : value + missingComponents;

            return Version.TryParse(normalized, out version);
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
