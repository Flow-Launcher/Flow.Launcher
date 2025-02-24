using Flow.Launcher.Core.ExternalPlugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using ISavable = Flow.Launcher.Plugin.ISavable;
using Flow.Launcher.Plugin.SharedCommands;
using System.Text.Json;
using Flow.Launcher.Core.Resource;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// The entry for managing Flow Launcher plugins
    /// </summary>
    public static class PluginManager
    {
        private static IEnumerable<PluginPair> _contextMenuPlugins;

        public static List<PluginPair> AllPlugins { get; private set; }
        public static readonly HashSet<PluginPair> GlobalPlugins = new();
        public static readonly Dictionary<string, PluginPair> NonGlobalPlugins = new();

        public static IPublicAPI API { get; private set; } = Ioc.Default.GetRequiredService<IPublicAPI>();

        private static PluginsSettings Settings;
        private static List<PluginMetadata> _metadatas;
        private static List<string> _modifiedPlugins = new List<string>();

        /// <summary>
        /// Directories that will hold Flow Launcher plugin directory
        /// </summary>
        private static readonly string[] Directories =
        {
            Constant.PreinstalledDirectory, DataLocation.PluginsDirectory
        };

        private static void DeletePythonBinding()
        {
            const string binding = "flowlauncher.py";
            foreach (var subDirectory in Directory.GetDirectories(DataLocation.PluginsDirectory))
            {
                File.Delete(Path.Combine(subDirectory, binding));
            }
        }

        /// <summary>
        /// Save json and ISavable
        /// </summary>
        public static void Save()
        {
            foreach (var plugin in AllPlugins)
            {
                var savable = plugin.Plugin as ISavable;
                savable?.Save();
            }

            API.SavePluginSettings();
        }

        public static async ValueTask DisposePluginsAsync()
        {
            foreach (var pluginPair in AllPlugins)
            {
                switch (pluginPair.Plugin)
                {
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                }
            }
        }

        public static async Task ReloadDataAsync()
        {
            await Task.WhenAll(AllPlugins.Select(plugin => plugin.Plugin switch
            {
                IReloadable p => Task.Run(p.ReloadData),
                IAsyncReloadable p => p.ReloadDataAsync(),
                _ => Task.CompletedTask,
            }).ToArray());
        }

        public static async Task OpenExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await Task.WhenAll(AllPlugins.Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.OpenPreviewAsync(path, sendFailToast),
                _ => Task.CompletedTask,
            }).ToArray());
        }

        public static async Task CloseExternalPreviewAsync()
        {
            await Task.WhenAll(AllPlugins.Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.ClosePreviewAsync(),
                _ => Task.CompletedTask,
            }).ToArray());
        }

        public static async Task SwitchExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await Task.WhenAll(AllPlugins.Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.SwitchPreviewAsync(path, sendFailToast),
                _ => Task.CompletedTask,
            }).ToArray());
        }

        public static bool UseExternalPreview()
        {
            return GetPluginsForInterface<IAsyncExternalPreview>().Any(x => !x.Metadata.Disabled);
        }

        public static bool AllowAlwaysPreview()
        {
            var plugin = GetPluginsForInterface<IAsyncExternalPreview>().FirstOrDefault(x => !x.Metadata.Disabled);

            if (plugin is null)
                return false;

            return ((IAsyncExternalPreview)plugin.Plugin).AllowAlwaysPreview();
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
        public static async Task InitializePluginsAsync()
        {
            var failedPlugins = new ConcurrentQueue<PluginPair>();

            var InitTasks = AllPlugins.Select(pair => Task.Run(async delegate
            {
                try
                {
                    var milliseconds = await Stopwatch.DebugAsync($"|PluginManager.InitializePlugins|Init method time cost for <{pair.Metadata.Name}>",
                        () => pair.Plugin.InitAsync(new PluginInitContext(pair.Metadata, API)));

                    pair.Metadata.InitTime += milliseconds;
                    Log.Info(
                        $"|PluginManager.InitializePlugins|Total init cost for <{pair.Metadata.Name}> is <{pair.Metadata.InitTime}ms>");
                }
                catch (Exception e)
                {
                    Log.Exception(nameof(PluginManager), $"Fail to Init plugin: {pair.Metadata.Name}", e);
                    pair.Metadata.Disabled = true;
                    failedPlugins.Enqueue(pair);
                }
            }));

            await Task.WhenAll(InitTasks);

            _contextMenuPlugins = GetPluginsForInterface<IContextMenu>();
            foreach (var plugin in AllPlugins)
            {
                // set distinct on each plugin's action keywords helps only firing global(*) and action keywords once where a plugin
                // has multiple global and action keywords because we will only add them here once.
                foreach (var actionKeyword in plugin.Metadata.ActionKeywords.Distinct())
                {
                    switch (actionKeyword)
                    {
                        case Query.GlobalPluginWildcardSign:
                            GlobalPlugins.Add(plugin);
                            break;
                        default:
                            NonGlobalPlugins[actionKeyword] = plugin;
                            break;
                    }
                }
            }

            InternationalizationManager.Instance.AddPluginLanguageDirectories(GetPluginsForInterface<IPluginI18n>());
            InternationalizationManager.Instance.ChangeLanguage(Ioc.Default.GetRequiredService<Settings>().Language);

            if (failedPlugins.Any())
            {
                var failed = string.Join(",", failedPlugins.Select(x => x.Metadata.Name));
                API.ShowMsg(
                    API.GetTranslation("failedToInitializePluginsTitle"),
                    string.Format(
                        API.GetTranslation("failedToInitializePluginsMessage"),
                        failed
                    ),
                    "",
                    false
                );
            }
        }

        public static ICollection<PluginPair> ValidPluginsForQuery(Query query)
        {
            if (query is null)
                return Array.Empty<PluginPair>();

            if (!NonGlobalPlugins.ContainsKey(query.ActionKeyword))
                return GlobalPlugins;


            var plugin = NonGlobalPlugins[query.ActionKeyword];
            return new List<PluginPair>
            {
                plugin
            };
        }

        public static async Task<List<Result>> QueryForPluginAsync(PluginPair pair, Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var metadata = pair.Metadata;

            try
            {
                var milliseconds = await Stopwatch.DebugAsync($"|PluginManager.QueryForPlugin|Cost for {metadata.Name}",
                    async () => results = await pair.Plugin.QueryAsync(query, token).ConfigureAwait(false));

                token.ThrowIfCancellationRequested();
                if (results == null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                metadata.QueryCount += 1;
                metadata.AvgQueryTime =
                    metadata.QueryCount == 1 ? milliseconds : (metadata.AvgQueryTime + milliseconds) / 2;
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }
            catch (Exception e)
            {
                Result r = new()
                {
                    Title = $"{metadata.Name}: Failed to respond!",
                    SubTitle = "Select this result for more info",
                    IcoPath = Flow.Launcher.Infrastructure.Constant.ErrorIcon,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ => { throw new FlowPluginException(metadata, e);},
                    Score = -100
                };
                results.Add(r);
            }
            return results;
        }

        public static void UpdatePluginMetadata(IReadOnlyList<Result> results, PluginMetadata metadata, Query query)
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
            // Handle scenario where this is called before all plugins are instantiated, e.g. language change on startup
            return AllPlugins?.Where(p => p.Plugin is T) ?? Array.Empty<PluginPair>();
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
                    results = plugin.LoadContextMenus(result) ?? results;
                    foreach (var r in results)
                    {
                        r.PluginDirectory = pluginPair.Metadata.PluginDirectory;
                        r.PluginID = pluginPair.Metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(
                        $"|PluginManager.GetContextMenusForPlugin|Can't load context menus for plugin <{pluginPair.Metadata.Name}>",
                        e);
                }
            }

            return results;
        }

        public static bool ActionKeywordRegistered(string actionKeyword)
        {
            // this method is only checking for action keywords (defined as not '*') registration
            // hence the actionKeyword != Query.GlobalPluginWildcardSign logic
            return actionKeyword != Query.GlobalPluginWildcardSign
                   && NonGlobalPlugins.ContainsKey(actionKeyword);
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
                NonGlobalPlugins[newActionKeyword] = plugin;
            }

            plugin.Metadata.ActionKeywords.Add(newActionKeyword);
        }

        /// <summary>
        /// used to remove action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public static void RemoveActionKeyword(string id, string oldActionkeyword)
        {
            var plugin = GetPluginForId(id);
            if (oldActionkeyword == Query.GlobalPluginWildcardSign
                && // Plugins may have multiple ActionKeywords that are global, eg. WebSearch
                plugin.Metadata.ActionKeywords
                    .Count(x => x == Query.GlobalPluginWildcardSign) == 1)
            {
                GlobalPlugins.Remove(plugin);
            }

            if (oldActionkeyword != Query.GlobalPluginWildcardSign)
                NonGlobalPlugins.Remove(oldActionkeyword);


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

        private static string GetContainingFolderPathAfterUnzip(string unzippedParentFolderPath)
        {
            var unzippedFolderCount = Directory.GetDirectories(unzippedParentFolderPath).Length;
            var unzippedFilesCount = Directory.GetFiles(unzippedParentFolderPath).Length;

            // adjust path depending on how the plugin is zipped up
            // the recommended should be to zip up the folder not the contents
            if (unzippedFolderCount == 1 && unzippedFilesCount == 0)
                // folder is zipped up, unzipped plugin directory structure: tempPath/unzippedParentPluginFolder/pluginFolderName/
                return Directory.GetDirectories(unzippedParentFolderPath)[0];

            if (unzippedFilesCount > 1)
                // content is zipped up, unzipped plugin directory structure: tempPath/unzippedParentPluginFolder/
                return unzippedParentFolderPath;

            return string.Empty;
        }

        private static bool SameOrLesserPluginVersionExists(string metadataPath)
        {
            var newMetadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(metadataPath));
            return AllPlugins.Any(x => x.Metadata.ID == newMetadata.ID
                          && newMetadata.Version.CompareTo(x.Metadata.Version) <= 0);
        }

        #region Public functions

        public static bool PluginModified(string uuid)
        {
            return _modifiedPlugins.Contains(uuid);
        }


        /// <summary>
        /// Update a plugin to new version, from a zip file. By default will remove the zip file if update is via url,
        /// unless it's a local path installation
        /// </summary>
        public static void UpdatePlugin(PluginMetadata existingVersion, UserPlugin newVersion, string zipFilePath)
        {
            InstallPlugin(newVersion, zipFilePath, checkModified:false);
            UninstallPlugin(existingVersion, removePluginFromSettings:false, removePluginSettings:false, checkModified: false);
            _modifiedPlugins.Add(existingVersion.ID);
        }

        /// <summary>
        /// Install a plugin. By default will remove the zip file if installation is from url, unless it's a local path installation
        /// </summary>
        public static void InstallPlugin(UserPlugin plugin, string zipFilePath)
        {
            InstallPlugin(plugin, zipFilePath, checkModified: true);
        }

        /// <summary>
        /// Uninstall a plugin.
        /// </summary>
        public static void UninstallPlugin(PluginMetadata plugin, bool removePluginFromSettings = true, bool removePluginSettings = false)
        {
            UninstallPlugin(plugin, removePluginFromSettings, removePluginSettings, true);
        }

        #endregion

        #region Internal functions

        internal static void InstallPlugin(UserPlugin plugin, string zipFilePath, bool checkModified)
        {
            if (checkModified && PluginModified(plugin.ID))
            {
                // Distinguish exception from installing same or less version
                throw new ArgumentException($"Plugin {plugin.Name} {plugin.ID} has been modified.", nameof(plugin));
            }

            // Unzip plugin files to temp folder
            var tempFolderPluginPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempFolderPluginPath);

            if(!plugin.IsFromLocalInstallPath)
                File.Delete(zipFilePath);

            var pluginFolderPath = GetContainingFolderPathAfterUnzip(tempFolderPluginPath);

            var metadataJsonFilePath = string.Empty;
            if (File.Exists(Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName)))
                metadataJsonFilePath = Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName);

            if (string.IsNullOrEmpty(metadataJsonFilePath) || string.IsNullOrEmpty(pluginFolderPath))
            {
                throw new FileNotFoundException($"Unable to find plugin.json from the extracted zip file, or this path {pluginFolderPath} does not exist");
            }

            if (SameOrLesserPluginVersionExists(metadataJsonFilePath))
            {
                throw new InvalidOperationException($"A plugin with the same ID and version already exists, or the version is greater than this downloaded plugin {plugin.Name}");
            }

            var folderName = string.IsNullOrEmpty(plugin.Version) ? $"{plugin.Name}-{Guid.NewGuid()}" : $"{plugin.Name}-{plugin.Version}";

            var defaultPluginIDs = new List<string>
                                    {
                                        "0ECADE17459B49F587BF81DC3A125110", // BrowserBookmark
                                        "CEA0FDFC6D3B4085823D60DC76F28855", // Calculator
                                        "572be03c74c642baae319fc283e561a8", // Explorer
                                        "6A122269676E40EB86EB543B945932B9", // PluginIndicator
                                        "9f8f9b14-2518-4907-b211-35ab6290dee7", // PluginsManager
                                        "b64d0a79-329a-48b0-b53f-d658318a1bf6", // ProcessKiller
                                        "791FC278BA414111B8D1886DFE447410", // Program
                                        "D409510CD0D2481F853690A07E6DC426", // Shell
                                        "CEA08895D2544B019B2E9C5009600DF4", // Sys
                                        "0308FD86DE0A4DEE8D62B9B535370992", // URL
                                        "565B73353DBF4806919830B9202EE3BF", // WebSearch
                                        "5043CETYU6A748679OPA02D27D99677A" // WindowsSettings
                                    };

            // Treat default plugin differently, it needs to be removable along with each flow release
            var installDirectory = !defaultPluginIDs.Any(x => x == plugin.ID)
                                    ? DataLocation.PluginsDirectory
                                    : Constant.PreinstalledDirectory;

            var newPluginPath = Path.Combine(installDirectory, folderName);

            FilesFolders.CopyAll(pluginFolderPath, newPluginPath, (s) => API.ShowMsgBox(s));

            try
            {
                if (Directory.Exists(tempFolderPluginPath))
                    Directory.Delete(tempFolderPluginPath, true);
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginManager.InstallPlugin|Failed to delete temp folder {tempFolderPluginPath}", e);
            }

            if (checkModified)
            {
                _modifiedPlugins.Add(plugin.ID);
            }
        }

        internal static void UninstallPlugin(PluginMetadata plugin, bool removePluginFromSettings, bool removePluginSettings, bool checkModified)
        {
            if (checkModified && PluginModified(plugin.ID))
            {
                throw new ArgumentException($"Plugin {plugin.Name} has been modified");
            }

            if (removePluginSettings)
            {
                if (AllowedLanguage.IsDotNet(plugin.Language))  // for the plugin in .NET, we can use assembly loader
                {
                    var assemblyLoader = new PluginAssemblyLoader(plugin.ExecuteFilePath);
                    var assembly = assemblyLoader.LoadAssemblyAndDependencies();
                    var assemblyName = assembly.GetName().Name;

                    // if user want to remove the plugin settings, we cannot call save method for the plugin json storage instance of this plugin
                    // so we need to remove it from the api instance
                    var method = API.GetType().GetMethod("RemovePluginSettings");
                    var pluginJsonStorage = method?.Invoke(API, new object[] { assemblyName });

                    // if there exists a json storage for current plugin, we need to delete the directory path
                    if (pluginJsonStorage != null)
                    {
                        var deleteMethod = pluginJsonStorage.GetType().GetMethod("DeleteDirectory");
                        try
                        {
                            deleteMethod?.Invoke(pluginJsonStorage, null);
                        }
                        catch (Exception e)
                        {
                            Log.Exception($"|PluginManager.UninstallPlugin|Failed to delete plugin json folder for {plugin.Name}", e);
                            API.ShowMsg(API.GetTranslation("failedToRemovePluginSettingsTitle"),
                                string.Format(API.GetTranslation("failedToRemovePluginSettingsMessage"), plugin.Name));
                        }
                    }
                }
                else  // the plugin with json prc interface
                {
                    var pluginPair = AllPlugins.FirstOrDefault(p => p.Metadata.ID == plugin.ID);
                    if (pluginPair != null && pluginPair.Plugin is JsonRPCPlugin jsonRpcPlugin)
                    {
                        try
                        {
                            jsonRpcPlugin.DeletePluginSettingsDirectory();
                        }
                        catch (Exception e)
                        {
                            Log.Exception($"|PluginManager.UninstallPlugin|Failed to delete plugin json folder for {plugin.Name}", e);
                            API.ShowMsg(API.GetTranslation("failedToRemovePluginSettingsTitle"),
                                string.Format(API.GetTranslation("failedToRemovePluginSettingsMessage"), plugin.Name));
                        }
                    }
                }
            }

            if (removePluginFromSettings)
            {
                Settings.Plugins.Remove(plugin.ID);
                AllPlugins.RemoveAll(p => p.Metadata.ID == plugin.ID);
            }

            // Marked for deletion. Will be deleted on next start up
            using var _ = File.CreateText(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt"));

            if (checkModified)
            {
                _modifiedPlugins.Add(plugin.ID);
            }
        }

        #endregion
    }
}
