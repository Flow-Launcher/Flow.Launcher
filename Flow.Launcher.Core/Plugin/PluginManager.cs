using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Microsoft.AspNetCore.Routing;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// The entry for managing Flow Launcher plugins
    /// </summary>
    public static class PluginManager
    {
        private static IEnumerable<PluginPair> _contextMenuPlugins;

        public static List<PluginPair> AllPlugins { get; private set; }
        public static readonly List<PluginPair> GlobalPlugins = new List<PluginPair>();
        public static readonly Dictionary<string, List<PluginPair>> NonGlobalPlugins = new Dictionary<string, List<PluginPair>>();

        public static IPublicAPI API { private set; get; }

        // todo happlebao, this should not be public, the indicator function should be embeded 
        public static PluginsSettings Settings;
        private static List<PluginMetadata> _metadatas;

        /// <summary>
        /// Directories that will hold Flow Launcher plugin directory
        /// </summary>
        private static readonly string[] Directories = { Constant.PreinstalledDirectory, DataLocation.PluginsDirectory };

        private static void DeletePythonBinding()
        {
            const string binding = "flowlauncher.py";
            foreach (var subDirectory in Directory.GetDirectories(DataLocation.PluginsDirectory))
            {
                File.Delete(Path.Combine(subDirectory, binding));
            }
        }

        public static void Save()
        {
            foreach (var plugin in AllPlugins)
            {
                var savable = plugin.Plugin as ISavable;
                savable?.Save();
            }
        }

        public static void ReloadData()
        {
            foreach (var plugin in AllPlugins)
            {
                var reloadablePlugin = plugin.Plugin as IReloadable;
                reloadablePlugin?.ReloadData();
            }
        }

        static PluginManager()
        {
            // validate user directory
            Directory.CreateDirectory(DataLocation.PluginsDirectory);
            // force old plugins use new python binding
            DeletePythonBinding();
        }

        /// <summary>
        /// because InitializePlugins needs API, so LoadPlugins needs to be called first
        /// todo happlebao The API should be removed
        /// </summary>
        /// <param name="settings"></param>
        public static void LoadPlugins(PluginsSettings settings)
        {
            _metadatas = PluginConfig.Parse(Directories);
            Settings = settings;
            Settings.UpdatePluginSettings(_metadatas);
            AllPlugins = PluginsLoader.Plugins(_metadatas, Settings);
        }

        /// <summary>
        /// Call initialize for all plugins
        /// </summary>
        /// <returns>return the list of failed to init plugins or null for none</returns>
        public static void InitializePlugins(IPublicAPI api)
        {
            API = api;
            var failedPlugins = new ConcurrentQueue<PluginPair>();
            Parallel.ForEach(AllPlugins, pair =>
            {
                try
                {
                    var milliseconds = Stopwatch.Debug($"|PluginManager.InitializePlugins|Init method time cost for <{pair.Metadata.Name}>", () =>
                    {
                        pair.Plugin.Init(new PluginInitContext
                        {
                            CurrentPluginMetadata = pair.Metadata,
                            API = API
                        });
                    });
                    pair.Metadata.InitTime += milliseconds;
                    Log.Info($"|PluginManager.InitializePlugins|Total init cost for <{pair.Metadata.Name}> is <{pair.Metadata.InitTime}ms>");
                }
                catch (Exception e)
                {
                    Log.Exception(nameof(PluginManager), $"Fail to Init plugin: {pair.Metadata.Name}", e);
                    pair.Metadata.Disabled = true;
                    failedPlugins.Enqueue(pair);
                }
            });

            _contextMenuPlugins = GetPluginsForInterface<IContextMenu>();
            foreach (var plugin in AllPlugins)
            {
                if (IsGlobalPlugin(plugin.Metadata))
                    GlobalPlugins.Add(plugin);

                // Plugins may have multiple ActionKeywords, eg. WebSearch
                plugin.Metadata.ActionKeywords
                    .Where(x => x != Query.GlobalPluginWildcardSign)
                    .ToList()
                    .ForEach(x =>
                    {
                        if (NonGlobalPlugins.ContainsKey(x))
                        {
                            NonGlobalPlugins[x].Add(plugin);
                        }
                        else
                        {
                            NonGlobalPlugins[x] = new List<PluginPair> { plugin };
                        }
                    });
            }

            if (failedPlugins.Any())
            {
                var failed = string.Join(",", failedPlugins.Select(x => x.Metadata.Name));
                API.ShowMsg($"Fail to Init Plugins", $"Plugins: {failed} - fail to load and would be disabled, please contact plugin creator for help", "", false);
            }
        }

        public static void InstallPlugin(string path)
        {
            PluginInstaller.Install(path);
        }

        public static List<PluginPair> ValidPluginsForQuery(Query query)
        {
            if (NonGlobalPlugins.ContainsKey(query.ActionKeyword))
            {
                return NonGlobalPlugins[query.ActionKeyword];
            }
            else
            {
                return GlobalPlugins;
            }
        }

        public static List<Result> QueryForPlugin(PluginPair pair, Query query)
        {
            var results = new List<Result>();
            try
            {
                var metadata = pair.Metadata;
                var milliseconds = Stopwatch.Debug($"|PluginManager.QueryForPlugin|Cost for {metadata.Name}", () =>
                {
                    results = pair.Plugin.Query(query) ?? new List<Result>();
                    UpdatePluginMetadata(results, metadata, query);
                });
                metadata.QueryCount += 1;
                metadata.AvgQueryTime = metadata.QueryCount == 1 ? milliseconds : (metadata.AvgQueryTime + milliseconds) / 2;
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginManager.QueryForPlugin|Exception for plugin <{pair.Metadata.Name}> when query <{query}>", e);
            }
            return results;
        }

        public static void UpdatePluginMetadata(List<Result> results, PluginMetadata metadata, Query query)
        {
            foreach (var r in results)
            {
                r.PluginDirectory = metadata.PluginDirectory;
                r.PluginID = metadata.ID;
                r.OriginQuery = query;

                // ActionKeywordAssigned is used for constructing MainViewModel's query text auto-complete suggestions 
                // Plugins may have multi-actionkeywords eg. WebSearches. In this scenario it needs to be overriden on the plugin level 
                if (metadata.ActionKeywords.Count == 1)
                    r.ActionKeywordAssigned = query.ActionKeyword;
            }
        }

        private static bool IsGlobalPlugin(PluginMetadata metadata)
        {
            return metadata.ActionKeywords.Contains(Query.GlobalPluginWildcardSign);
        }

        /// <summary>
        /// get specified plugin, return null if not found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PluginPair GetPluginForId(string id)
        {
            return AllPlugins.FirstOrDefault(o => o.Metadata.ID == id);
        }

        public static IEnumerable<PluginPair> GetPluginsForInterface<T>() where T : IFeatures
        {
            return AllPlugins.Where(p => p.Plugin is T);
        }

        public static List<Result> GetContextMenusForPlugin(Result result)
        {
            var results = new List<Result>();
            var pluginPair = _contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            if (pluginPair != null)
            {
                var plugin = (IContextMenu)pluginPair.Plugin;

                try
                {
                    results = plugin.LoadContextMenus(result);
                    foreach (var r in results)
                    {
                        r.PluginDirectory = pluginPair.Metadata.PluginDirectory;
                        r.PluginID = pluginPair.Metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }
                }
                catch (Exception e)
                {
                    Log.Exception($"|PluginManager.GetContextMenusForPlugin|Can't load context menus for plugin <{pluginPair.Metadata.Name}>", e);
                }
            }
            return results;
        }


        /// <summary>
        /// used to add action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public static void AddActionKeyword(string id, string newActionKeyword)
        {
            var plugin = GetPluginForId(id);
            if (newActionKeyword == Query.GlobalPluginWildcardSign)
            {
                GlobalPlugins.Add(plugin);
            }
            else
            {
                if (NonGlobalPlugins.ContainsKey(newActionKeyword))
                {
                    NonGlobalPlugins[newActionKeyword].Add(plugin);
                }
                else
                {
                    NonGlobalPlugins[newActionKeyword] = new List<PluginPair> { plugin };
                }
            }
            plugin.Metadata.ActionKeywords.Add(newActionKeyword);
        }

        /// <summary>
        /// used to add action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public static void RemoveActionKeyword(string id, string oldActionkeyword)
        {
            var plugin = GetPluginForId(id);
            if (oldActionkeyword == Query.GlobalPluginWildcardSign
                && // Plugins may have multiple ActionKeywords that are global, eg. WebSearch
                plugin.Metadata.ActionKeywords
                                    .Where(x => x == Query.GlobalPluginWildcardSign)
                                    .Count() == 1)
            {
                GlobalPlugins.Remove(plugin);
            }

            if (oldActionkeyword != Query.GlobalPluginWildcardSign
                && plugin.Metadata.ActionKeywords.Where(x => x == oldActionkeyword).Count() == 1)
            {
                NonGlobalPlugins[oldActionkeyword].Remove(plugin);
            }


            plugin.Metadata.ActionKeywords.Remove(oldActionkeyword);
        }

        public static void ReplaceActionKeyword(string id, string oldActionKeyword, string newActionKeyword)
        {
            if (oldActionKeyword != newActionKeyword)
            {
                AddActionKeyword(id, newActionKeyword);
                RemoveActionKeyword(id, oldActionKeyword);
            }
        }
    }
}
