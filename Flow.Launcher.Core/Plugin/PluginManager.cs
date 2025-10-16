using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using IRemovable = Flow.Launcher.Core.Storage.IRemovable;
using ISavable = Flow.Launcher.Plugin.ISavable;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Class for co-ordinating and managing all plugin lifecycle.
    /// </summary>
    public static class PluginManager
    {
        private static readonly string ClassName = nameof(PluginManager);

        private static readonly ConcurrentDictionary<string, PluginPair> _allLoadedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _allInitializedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _initFailedPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _globalPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _nonGlobalPlugins = [];

        private static PluginsSettings Settings;
        private static readonly ConcurrentBag<string> ModifiedPlugins = [];

        private static readonly ConcurrentBag<PluginPair> _contextMenuPlugins = [];
        private static readonly ConcurrentBag<PluginPair> _homePlugins = [];
        private static readonly ConcurrentBag<PluginPair> _translationPlugins = [];
        private static readonly ConcurrentBag<PluginPair> _externalPreviewPlugins = [];

        /// <summary>
        /// Directories that will hold Flow Launcher plugin directory
        /// </summary>
        public static readonly string[] Directories =
        [
            Constant.PreinstalledDirectory, DataLocation.PluginsDirectory
        ];

        #region Save & Dispose & Reload Plugin

        /// <summary>
        /// Save json and ISavable
        /// </summary>
        public static void Save()
        {
            foreach (var pluginPair in GetAllInitializedPlugins(includeFailed: false))
            {
                var savable = pluginPair.Plugin as ISavable;
                try
                {
                    savable?.Save();
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Failed to save plugin {pluginPair.Metadata.Name}", e);
                }
            }

            PublicApi.Instance.SavePluginSettings();
            PublicApi.Instance.SavePluginCaches();
        }

        public static async ValueTask DisposePluginsAsync()
        {
            // Still call dispose for all plugins even if initialization failed, so that we can clean up resources
            foreach (var pluginPair in GetAllInitializedPlugins(includeFailed: true))
            {
                await DisposePluginAsync(pluginPair);
            }
        }

        private static async Task DisposePluginAsync(PluginPair pluginPair)
        {
            try
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
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed to dispose plugin {pluginPair.Metadata.Name}", e);
            }
        }

        public static async Task ReloadDataAsync()
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IReloadable p => Task.Run(p.ReloadData),
                IAsyncReloadable p => p.ReloadDataAsync(),
                _ => Task.CompletedTask,
            })]);
        }

        #endregion

        #region External Preview

        public static async Task OpenExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.OpenPreviewAsync(path, sendFailToast),
                _ => Task.CompletedTask,
            })]);
        }

        public static async Task CloseExternalPreviewAsync()
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.ClosePreviewAsync(),
                _ => Task.CompletedTask,
            })]);
        }

        public static async Task SwitchExternalPreviewAsync(string path, bool sendFailToast = true)
        {
            await Task.WhenAll([.. GetAllInitializedPlugins(includeFailed: false).Select(plugin => plugin.Plugin switch
            {
                IAsyncExternalPreview p => p.SwitchPreviewAsync(path, sendFailToast),
                _ => Task.CompletedTask,
            })]);
        }

        public static bool UseExternalPreview()
        {
            return GetExternalPreviewPlugins().Any(x => !x.Metadata.Disabled);
        }

        public static bool AllowAlwaysPreview()
        {
            var plugin = GetExternalPreviewPlugins().FirstOrDefault(x => !x.Metadata.Disabled);

            if (plugin is null)
                return false;

            return ((IAsyncExternalPreview)plugin.Plugin).AllowAlwaysPreview();
        }

        private static IList<PluginPair> GetExternalPreviewPlugins()
        {
            return [.. _externalPreviewPlugins.Where(p => !PluginModified(p.Metadata.ID))];
        }

        #endregion

        #region Constructor

        static PluginManager()
        {
            // validate user directory
            Directory.CreateDirectory(DataLocation.PluginsDirectory);
            // force old plugins use new python binding
            DeletePythonBinding();
        }

        private static void DeletePythonBinding()
        {
            const string binding = "flowlauncher.py";
            foreach (var subDirectory in Directory.GetDirectories(DataLocation.PluginsDirectory))
            {
                try
                {
                    File.Delete(Path.Combine(subDirectory, binding));
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogDebug(ClassName, $"Failed to delete {binding} in {subDirectory}: {e.Message}");
                }
            }
        }

        #endregion

        #region Load & Initialize Plugins

        /// <summary>
        /// Load plugins from the directories specified in Directories.
        /// </summary>
        /// <param name="settings"></param>
        public static void LoadPlugins(PluginsSettings settings)
        {
            var metadatas = PluginConfig.Parse(Directories);
            Settings = settings;
            Settings.UpdatePluginSettings(metadatas);

            // Load plugins
            var allLoadedPlugins = PluginsLoader.Plugins(metadatas, Settings);
            foreach (var plugin in allLoadedPlugins)
            {
                if (plugin != null)
                {
                    if (!_allLoadedPlugins.TryAdd(plugin.Metadata.ID, plugin))
                    {
                        PublicApi.Instance.LogError(ClassName, $"Plugin with ID {plugin.Metadata.ID} already loaded");
                    }
                }
            }

            // Since dotnet plugins need to get assembly name first, we should update plugin directory after loading plugins
            UpdatePluginDirectory(metadatas);
        }

        private static void UpdatePluginDirectory(List<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (AllowedLanguage.IsDotNet(metadata.Language))
                {
                    if (string.IsNullOrEmpty(metadata.AssemblyName))
                    {
                        PublicApi.Instance.LogWarn(ClassName, $"AssemblyName is empty for plugin with metadata: {metadata.Name}");
                        continue; // Skip if AssemblyName is not set, which can happen for erroneous plugins
                    }
                    metadata.PluginSettingsDirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, metadata.AssemblyName);
                    metadata.PluginCacheDirectoryPath = Path.Combine(DataLocation.PluginCacheDirectory, metadata.AssemblyName);
                }
                else
                {
                    if (string.IsNullOrEmpty(metadata.Name))
                    {
                        PublicApi.Instance.LogWarn(ClassName, $"Name is empty for plugin with metadata: {metadata.Name}");
                        continue; // Skip if Name is not set, which can happen for erroneous plugins
                    }
                    metadata.PluginSettingsDirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, metadata.Name);
                    metadata.PluginCacheDirectoryPath = Path.Combine(DataLocation.PluginCacheDirectory, metadata.Name);
                }
            }
        }

        /// <summary>
        /// Initialize all plugins asynchronously.
        /// </summary>
        /// <param name="register">The register to register results updated event for each plugin.</param>
        /// <returns>return the list of failed to init plugins or null for none</returns>
        public static async Task InitializePluginsAsync(IResultUpdateRegister register)
        {
            var initTasks = _allLoadedPlugins.Select(x => Task.Run(async () =>
            {
                var pair = x.Value;

                // Register plugin action keywords so that plugins can be queried in results
                RegisterPluginActionKeywords(pair);

                try
                {
                    var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Init method time cost for <{pair.Metadata.Name}>",
                        () => pair.Plugin.InitAsync(new PluginInitContext(pair.Metadata, PublicApi.Instance)));

                    pair.Metadata.InitTime += milliseconds;
                    PublicApi.Instance.LogInfo(ClassName,
                        $"Total init cost for <{pair.Metadata.Name}> is <{pair.Metadata.InitTime}ms>");
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Fail to Init plugin: {pair.Metadata.Name}", e);
                    if (pair.Metadata.Disabled && pair.Metadata.HomeDisabled)
                    {
                        // If this plugin is already disabled, do not show error message again
                        // Or else it will be shown every time
                        PublicApi.Instance.LogDebug(ClassName, $"Skipped init for <{pair.Metadata.Name}> due to error");
                    }
                    else
                    {
                        pair.Metadata.Disabled = true;
                        pair.Metadata.HomeDisabled = true;
                        PublicApi.Instance.LogDebug(ClassName, $"Disable plugin <{pair.Metadata.Name}> because init failed");
                    }

                    // Even if the plugin cannot be initialized, we still need to add it in all plugin list so that
                    // we can remove the plugin from Plugin or Store page or Plugin Manager plugin.
                    _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
                    _initFailedPlugins.TryAdd(pair.Metadata.ID, pair);
                    return;
                }

                // Register ResultsUpdated event so that plugin query can use results updated interface
                register.RegisterResultsUpdatedEvent(pair);

                // Update plugin metadata translation after the plugin is initialized with IPublicAPI instance
                Internationalization.UpdatePluginMetadataTranslation(pair);

                // Add plugin to Dialog Jump plugin list after the plugin is initialized
                DialogJump.InitializeDialogJumpPlugin(pair);

                // Add plugin to lists after the plugin is initialized
                AddPluginToLists(pair);
            }));

            await Task.WhenAll(initTasks);

            if (!_initFailedPlugins.IsEmpty)
            {
                var failed = string.Join(",", _initFailedPlugins.Values.Select(x => x.Metadata.Name));
                PublicApi.Instance.ShowMsg(
                    Localize.failedToInitializePluginsTitle(),
                    Localize.failedToInitializePluginsMessage(failed),
                    "",
                    false
                );
            }
        }

        private static void RegisterPluginActionKeywords(PluginPair pair)
        {
            // set distinct on each plugin's action keywords helps only firing global(*) and action keywords once where a plugin
            // has multiple global and action keywords because we will only add them here once.
            foreach (var actionKeyword in pair.Metadata.ActionKeywords.Distinct())
            {
                switch (actionKeyword)
                {
                    case Query.GlobalPluginWildcardSign:
                        _globalPlugins.TryAdd(pair.Metadata.ID, pair);
                        break;
                    default:
                        _nonGlobalPlugins.TryAdd(actionKeyword, pair);
                        break;
                }
            }
        }

        private static void AddPluginToLists(PluginPair pair)
        {
            if (pair.Plugin is IContextMenu)
            {
                _contextMenuPlugins.Add(pair);
            }
            if (pair.Plugin is IAsyncHomeQuery)
            {
                _homePlugins.Add(pair);
            }
            if (pair.Plugin is IPluginI18n)
            {
                _translationPlugins.Add(pair);
            }
            if (pair.Plugin is IAsyncExternalPreview)
            {
                _externalPreviewPlugins.Add(pair);
            }
            _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
        }

        #endregion

        #region Validate & Query Plugins

        public static ICollection<PluginPair> ValidPluginsForQuery(Query query, bool dialogJump)
        {
            if (query is null)
                return Array.Empty<PluginPair>();

            if (!_nonGlobalPlugins.TryGetValue(query.ActionKeyword, out var plugin))
            {
                if (dialogJump)
                    return [.. GetGlobalPlugins().Where(p => p.Plugin is IAsyncDialogJump && !PluginModified(p.Metadata.ID))];
                else
                    return [.. GetGlobalPlugins().Where(p => !PluginModified(p.Metadata.ID))];
            }

            if (dialogJump && plugin.Plugin is not IAsyncDialogJump)
                return Array.Empty<PluginPair>();

            if (PluginModified(plugin.Metadata.ID))
                return Array.Empty<PluginPair>();

            return [plugin];
        }

        public static ICollection<PluginPair> ValidPluginsForHomeQuery()
        {
            return [.. _homePlugins.Where(p => !PluginModified(p.Metadata.ID))];
        }

        public static async Task<List<Result>> QueryForPluginAsync(PluginPair pair, Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var metadata = pair.Metadata;

            if (IsPluginInitializing(metadata))
            {
                Result r = new()
                {
                    Title = Localize.pluginStillInitializing(metadata.Name),
                    SubTitle = Localize.pluginStillInitializingSubtitle(),
                    AutoCompleteText = query.RawQuery,
                    IcoPath = metadata.IcoPath,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ =>
                    {
                        PublicApi.Instance.ReQuery();
                        return false;
                    }
                };
                results.Add(r);
                return results;
            }

            try
            {
                var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Cost for {metadata.Name}",
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
                    Title = Localize.pluginFailedToRespond(metadata.Name),
                    SubTitle = Localize.pluginFailedToRespondSubtitle(),
                    AutoCompleteText = query.RawQuery,
                    IcoPath = Constant.ErrorIcon,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ => { throw new FlowPluginException(metadata, e);}
                };
                results.Add(r);
            }
            return results;
        }

        public static async Task<List<Result>> QueryHomeForPluginAsync(PluginPair pair, Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var metadata = pair.Metadata;

            if (IsPluginInitializing(metadata))
            {
                Result r = new()
                {
                    Title = Localize.pluginStillInitializing(metadata.Name),
                    SubTitle = Localize.pluginStillInitializingSubtitle(),
                    AutoCompleteText = query.RawQuery,
                    IcoPath = metadata.IcoPath,
                    PluginDirectory = metadata.PluginDirectory,
                    ActionKeywordAssigned = query.ActionKeyword,
                    PluginID = metadata.ID,
                    OriginQuery = query,
                    Action = _ =>
                    {
                        PublicApi.Instance.ReQuery();
                        return false;
                    }
                };
                results.Add(r);
                return results;
            }

            try
            {
                var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Cost for {metadata.Name}",
                    async () => results = await ((IAsyncHomeQuery)pair.Plugin).HomeQueryAsync(token).ConfigureAwait(false));

                token.ThrowIfCancellationRequested();
                if (results == null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed to query home for plugin: {metadata.Name}", e);
                return null;
            }
            return results;
        }

        public static async Task<List<DialogJumpResult>> QueryDialogJumpForPluginAsync(PluginPair pair, Query query, CancellationToken token)
        {
            var results = new List<DialogJumpResult>();
            var metadata = pair.Metadata;

            if (IsPluginInitializing(metadata))
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }

            try
            {
                var milliseconds = await PublicApi.Instance.StopwatchLogDebugAsync(ClassName, $"Cost for {metadata.Name}",
                    async () => results = await ((IAsyncDialogJump)pair.Plugin).QueryDialogJumpAsync(query, token).ConfigureAwait(false));

                token.ThrowIfCancellationRequested();
                if (results == null)
                    return null;
                UpdatePluginMetadata(results, metadata, query);

                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // null will be fine since the results will only be added into queue if the token hasn't been cancelled
                return null;
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed to query Dialog Jump for plugin: {metadata.Name}", e);
                return null;
            }
            return results;
        }

        private static bool IsPluginInitializing(PluginMetadata metadata)
        {
            return !_allInitializedPlugins.ContainsKey(metadata.ID);
        }

        #endregion

        #region Get Plugin List

        public static List<PluginPair> GetAllLoadedPlugins()
        {
            return [.. _allLoadedPlugins.Values];
        }

        public static List<PluginPair> GetAllInitializedPlugins(bool includeFailed)
        {
            if (includeFailed)
            {
                return [.. _allInitializedPlugins.Values];
            }
            else
            {
                return [.. _allInitializedPlugins.Values
                    .Where(p => !_initFailedPlugins.ContainsKey(p.Metadata.ID))];
            }
        }

        private static List<PluginPair> GetGlobalPlugins()
        {
            return [.. _globalPlugins.Values];
        }

        public static Dictionary<string, PluginPair> GetNonGlobalPlugins()
        {
            return _nonGlobalPlugins.ToDictionary();
        }

        public static List<PluginPair> GetTranslationPlugins()
        {
            return [.. _translationPlugins.Where(p => !PluginModified(p.Metadata.ID))];
        }

        #endregion

        #region Update Metadata & Get Plugin

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
        /// <remarks>
        /// Plugin may not be initialized, so do not use its plugin model to execute any commands
        /// </remarks>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PluginPair GetPluginForId(string id)
        {
            return GetAllLoadedPlugins().FirstOrDefault(o => o.Metadata.ID == id);
        }

        #endregion

        #region Get Context Menus

        public static List<Result> GetContextMenusForPlugin(Result result)
        {
            var results = new List<Result>();
            var pluginPair = _contextMenuPlugins.Where(p => !PluginModified(p.Metadata.ID)).FirstOrDefault(o => o.Metadata.ID == result.PluginID);
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
                    PublicApi.Instance.LogException(ClassName, 
                        $"Can't load context menus for plugin <{pluginPair.Metadata.Name}>",
                        e);
                }
            }

            return results;
        }

        #endregion

        #region Check Home Plugin

        public static bool IsHomePlugin(string id)
        {
            return _homePlugins.Where(p => !PluginModified(p.Metadata.ID)).Any(p => p.Metadata.ID == id);
        }

        #endregion

        #region Check Initializing & Init Failed

        public static bool IsInitializingOrInitFailed(string id)
        {
            // Id does not exist in loaded plugins
            if (!_allLoadedPlugins.ContainsKey(id)) return false;

            // Plugin initialized already
            if (_allInitializedPlugins.ContainsKey(id))
            {
                // Check if the plugin initialization failed
                return _initFailedPlugins.ContainsKey(id);
            }
            // Plugin is still initializing
            else
            {
                return true;
            }
        }

        public static bool IsInitializing(string id)
        {
            // Id does not exist in loaded plugins
            if (!_allLoadedPlugins.ContainsKey(id)) return false;

            // Plugin initialized already
            if (_allInitializedPlugins.ContainsKey(id))
            {
                return false;
            }
            // Plugin is still initializing
            else
            {
                return true;
            }
        }

        public static bool IsInitializationFailed(string id)
        {
            // Id does not exist in loaded plugins
            if (!_allLoadedPlugins.ContainsKey(id)) return false;

            // Plugin initialized already
            if (_allInitializedPlugins.ContainsKey(id))
            {
                // Check if the plugin initialization failed
                return _initFailedPlugins.ContainsKey(id);
            }
            // Plugin is still initializing
            else
            {
                return false;
            }
        }

        #endregion

        #region Plugin Action Keyword

        public static bool ActionKeywordRegistered(string actionKeyword)
        {
            // this method is only checking for action keywords (defined as not '*') registration
            // hence the actionKeyword != Query.GlobalPluginWildcardSign logic
            return actionKeyword != Query.GlobalPluginWildcardSign 
                && _nonGlobalPlugins.ContainsKey(actionKeyword);
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
                _globalPlugins.TryAdd(id, plugin);
            }
            else
            {
                _nonGlobalPlugins.AddOrUpdate(newActionKeyword, plugin, (key, oldValue) => plugin);
            }

            // Update action keywords and action keyword in plugin metadata
            plugin.Metadata.ActionKeywords.Add(newActionKeyword);
            if (plugin.Metadata.ActionKeywords.Count > 0)
            {
                plugin.Metadata.ActionKeyword = plugin.Metadata.ActionKeywords[0];
            }
            else
            {
                plugin.Metadata.ActionKeyword = string.Empty;
            }
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
                _globalPlugins.TryRemove(id, out _);
            }

            if (oldActionkeyword != Query.GlobalPluginWildcardSign)
            {
                _nonGlobalPlugins.TryRemove(oldActionkeyword, out _);
            }

            // Update action keywords and action keyword in plugin metadata
            plugin.Metadata.ActionKeywords.Remove(oldActionkeyword);
            if (plugin.Metadata.ActionKeywords.Count > 0)
            {
                plugin.Metadata.ActionKeyword = plugin.Metadata.ActionKeywords[0];
            }
            else
            {
                plugin.Metadata.ActionKeyword = string.Empty;
            }
        }

        #endregion

        #region Plugin Install & Uninstall & Update

        #region Private Functions

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

            if (!Version.TryParse(newMetadata.Version, out var newVersion))
                return true; // If version is not valid, we assume it is lesser than any existing version

            // Get all plugins even if initialization failed so that we can check if the plugin with the same ID exists
            return GetAllInitializedPlugins(includeFailed: true).Any(x => x.Metadata.ID == newMetadata.ID
                && Version.TryParse(x.Metadata.Version, out var version)
                && newVersion <= version);
        }

        #endregion

        #region Public Functions

        public static bool PluginModified(string id)
        {
            return ModifiedPlugins.Contains(id);
        }

        public static async Task<bool> UpdatePluginAsync(PluginMetadata existingVersion, UserPlugin newVersion, string zipFilePath)
        {
            if (PluginModified(existingVersion.ID))
            {
                PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(existingVersion.Name),
                    Localize.pluginModifiedAlreadyMessage());
                return false;
            }

            var installSuccess = InstallPlugin(newVersion, zipFilePath, checkModified: false);
            if (!installSuccess) return false;

            var uninstallSuccess = await UninstallPluginAsync(existingVersion, removePluginFromSettings: false, removePluginSettings: false, checkModified: false);
            if (!uninstallSuccess) return false;

            ModifiedPlugins.Add(existingVersion.ID);
            return true;
        }

        public static bool InstallPlugin(UserPlugin plugin, string zipFilePath)
        {
            return InstallPlugin(plugin, zipFilePath, checkModified: true);
        }

        public static async Task<bool> UninstallPluginAsync(PluginMetadata plugin, bool removePluginSettings = false)
        {
            return await UninstallPluginAsync(plugin, removePluginFromSettings: true, removePluginSettings: removePluginSettings, checkModified: true);
        }

        #endregion

        #region Internal Functions

        internal static bool InstallPlugin(UserPlugin plugin, string zipFilePath, bool checkModified)
        {
            if (checkModified && PluginModified(plugin.ID))
            {
                PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(plugin.Name),
                    Localize.pluginModifiedAlreadyMessage());
                return false;
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
                PublicApi.Instance.ShowMsgError(Localize.failedToInstallPluginTitle(plugin.Name),
                    Localize.fileNotFoundMessage(pluginFolderPath));
                return false;
            }

            if (SameOrLesserPluginVersionExists(metadataJsonFilePath))
            {
                PublicApi.Instance.ShowMsgError(Localize.failedToInstallPluginTitle(plugin.Name),
                    Localize.pluginExistAlreadyMessage());
                return false;
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

            FilesFolders.CopyAll(pluginFolderPath, newPluginPath, (s) => PublicApi.Instance.ShowMsgBox(s));

            try
            {
                if (Directory.Exists(tempFolderPluginPath))
                    Directory.Delete(tempFolderPluginPath, true);
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed to delete temp folder {tempFolderPluginPath}", e);
            }

            if (checkModified)
            {
                ModifiedPlugins.Add(plugin.ID);
            }

            return true;
        }

        internal static async Task<bool> UninstallPluginAsync(PluginMetadata plugin, bool removePluginFromSettings, bool removePluginSettings, bool checkModified)
        {
            if (checkModified && PluginModified(plugin.ID))
            {
                PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(plugin.Name),
                    Localize.pluginModifiedAlreadyMessage());
                return false;
            }

            if (removePluginSettings || removePluginFromSettings)
            {
                // If we want to remove plugin from AllPlugins,
                // we need to dispose them so that they can release file handles
                // which can help FL to delete the plugin settings & cache folders successfully
                var pluginPairs = GetAllInitializedPlugins(includeFailed: true).Where(p => p.Metadata.ID == plugin.ID).ToList();
                foreach (var pluginPair in pluginPairs)
                {
                    await DisposePluginAsync(pluginPair);
                }
            }

            if (removePluginSettings)
            {
                // For dotnet plugins, we need to remove their PluginJsonStorage and PluginBinaryStorage instances
                if (AllowedLanguage.IsDotNet(plugin.Language) && PublicApi.Instance is IRemovable removable)
                {
                    removable.RemovePluginSettings(plugin.AssemblyName);
                    removable.RemovePluginCaches(plugin.PluginCacheDirectoryPath);
                }

                try
                {
                    var pluginSettingsDirectory = plugin.PluginSettingsDirectoryPath;
                    if (Directory.Exists(pluginSettingsDirectory))
                        Directory.Delete(pluginSettingsDirectory, true);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Failed to delete plugin settings folder for {plugin.Name}", e);
                    PublicApi.Instance.ShowMsgError(Localize.failedToRemovePluginSettingsTitle(),
                        Localize.failedToRemovePluginSettingsMessage(plugin.Name));
                }
            }

            if (removePluginFromSettings)
            {
                try
                {
                    var pluginCacheDirectory = plugin.PluginCacheDirectoryPath;
                    if (Directory.Exists(pluginCacheDirectory))
                        Directory.Delete(pluginCacheDirectory, true);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Failed to delete plugin cache folder for {plugin.Name}", e);
                    PublicApi.Instance.ShowMsgError(Localize.failedToRemovePluginCacheTitle(),
                        Localize.failedToRemovePluginCacheMessage(plugin.Name));
                }
                Settings.RemovePluginSettings(plugin.ID);
                {
                    _allLoadedPlugins.TryRemove(plugin.ID, out var _);
                }
                {
                    _allInitializedPlugins.TryRemove(plugin.ID, out var _);
                }
                {
                    _initFailedPlugins.TryRemove(plugin.ID, out var _);
                }
                {
                    _globalPlugins.TryRemove(plugin.ID, out var _);
                }
                var keysToRemove = _nonGlobalPlugins.Where(p => p.Value.Metadata.ID == plugin.ID).Select(p => p.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    _nonGlobalPlugins.TryRemove(key, out var _);
                }
            }

            // Marked for deletion. Will be deleted on next start up
            using var _ = File.CreateText(Path.Combine(plugin.PluginDirectory, DataLocation.PluginDeleteFile));

            if (checkModified)
            {
                ModifiedPlugins.Add(plugin.ID);
            }

            return true;
        }

        #endregion

        #endregion
    }
}
