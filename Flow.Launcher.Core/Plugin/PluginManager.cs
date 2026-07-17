using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
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
        private static readonly ConcurrentDictionary<string, List<PluginPair>> _nonGlobalPlugins = [];

        private static PluginsSettings Settings;
        private static IResultUpdateRegister _register;
        private static readonly ConcurrentDictionary<string, byte> ModifiedPlugins = [];

        // Load contexts of dotnet plugins, kept so their assemblies can be unloaded on reload/uninstall
        private static readonly ConcurrentDictionary<string, PluginAssemblyLoader> _assemblyLoaders = [];

        private static readonly ConcurrentDictionary<string, PluginPair> _contextMenuPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _homePlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _translationPlugins = [];
        private static readonly ConcurrentDictionary<string, PluginPair> _externalPreviewPlugins = [];

        /// <summary>
        /// Directories that will hold Flow Launcher plugin directory
        /// </summary>
        public static readonly string[] Directories =
        [
            Constant.PreinstalledDirectory, DataLocation.PluginsDirectory
        ];

        internal static void TrackAssemblyLoader(string id, PluginAssemblyLoader loader)
        {
            _assemblyLoaders[id] = loader;
        }

        private static bool HotReloadEnabled =>
            Ioc.Default.GetRequiredService<Infrastructure.UserSettings.Settings>().HotReloadAfterChanging;

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

        #region Hot Reload Plugin

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _reloadLocks = new();

        // Directories of freshly installed or updated plugins that have not been loaded yet,
        // so that ReloadPluginAsync knows to load the new version instead of the running one
        private static readonly ConcurrentDictionary<string, string> _pendingInstallPaths = new();

        /// <summary>
        /// Fully reloads one plugin in place: disposes the instance, unloads its assembly (for dotnet
        /// plugins), and loads and initializes the plugin again from disk. If the plugin was just
        /// installed or updated, the newly installed version is loaded. On failure the plugin is
        /// flagged as modified so the existing restart-required flow takes over.
        /// </summary>
        public static async Task<bool> ReloadPluginAsync(string id)
        {
            var semaphore = _reloadLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                string pluginDirectory;
                var fromPendingInstall = false;
                if (_pendingInstallPaths.TryRemove(id, out var newDirectory) && Directory.Exists(newDirectory))
                {
                    // Freshly installed or updated: unload the running version if any, load the new directory
                    pluginDirectory = newDirectory;
                    fromPendingInstall = true;
                    if (_allLoadedPlugins.TryGetValue(id, out var oldPair))
                    {
                        await UnloadPluginAsync(oldPair);
                    }
                }
                else if (_allLoadedPlugins.TryGetValue(id, out var pair))
                {
                    pluginDirectory = pair.Metadata.PluginDirectory;
                    await UnloadPluginAsync(pair);
                }
                else
                {
                    return false;
                }

                var success = await LoadAndInitializePluginAsync(pluginDirectory);
                if (success)
                {
                    ClearPluginModified(id);
                }
                else
                {
                    // The plugin is unloaded at this point, so keep its directory discoverable
                    // in either case: a later reload attempt can then still pick it up
                    _pendingInstallPaths.TryAdd(id, pluginDirectory);
                    ModifiedPlugins.TryAdd(id, 0);
                }
                return success;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Fully reloads all loaded plugins. Plugins already flagged as modified are skipped because
        /// their on-disk directory is pending deletion or replacement and requires a restart.
        /// Returns false if any plugin failed to reload.
        /// </summary>
        public static async Task<bool> ReloadAllPluginsAsync()
        {
            var allSucceeded = true;
            foreach (var pair in GetAllLoadedPlugins())
            {
                var id = pair.Metadata.ID;
                if (PluginModified(id)) continue;

                if (!await ReloadPluginAsync(id))
                {
                    allSucceeded = false;
                }
            }
            return allSucceeded;
        }

        /// <summary>
        /// Removes a plugin from every runtime registry, disposes it and, for dotnet plugins, unloads
        /// its assembly load context. Returns false if the assembly could not be verified as unloaded,
        /// in which case it stays in memory (and its files locked) until the app restarts.
        /// </summary>
        internal static async Task<bool> UnloadPluginAsync(PluginPair pair)
        {
            var metadata = pair.Metadata;
            var id = metadata.ID;

            // Save plugin settings before the instance is disposed
            try
            {
                (pair.Plugin as ISavable)?.Save();
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed to save plugin {metadata.Name} before unload", e);
            }

            // Removing the plugin from the initialized list first makes in-flight queries treat it as
            // still initializing, so they show a placeholder result instead of touching a dead instance
            RemovePluginFromLists(id);
            UnregisterPluginActionKeywords(id);
            _register?.UnregisterResultsUpdatedEvent(pair);
            DialogJump.RemoveDialogJumpPlugin(pair);
            _allLoadedPlugins.TryRemove(id, out _);

            // For JsonRPC V2 plugins this kills the child process, releasing its file locks
            await DisposePluginAsync(pair);

            if (!_assemblyLoaders.TryRemove(id, out var loader)) return true;

            // Persist and evict the Type-keyed storages that would otherwise pin the collectible load context
            PublicApi.Instance.SavePluginSettings();
            PublicApi.Instance.SavePluginCaches();
            if (PublicApi.Instance is IRemovable removable)
            {
                removable.RemovePluginSettings(metadata.AssemblyName);
                removable.RemovePluginCaches(metadata.PluginCacheDirectoryPath);
            }

            pair.Plugin = null;

            var weakReference = PluginAssemblyLoader.UnloadAndGetWeakReference(loader);
            loader = null;
            var unloaded = await PluginAssemblyLoader.WaitForUnloadAsync(weakReference);
            if (!unloaded)
            {
                PublicApi.Instance.LogWarn(ClassName,
                    $"Assembly of plugin <{metadata.Name}> could not be fully unloaded, e.g. because results " +
                    $"or event handlers still reference it. Its memory will be reclaimed on restart.");
            }

            return unloaded;
        }

        /// <summary>
        /// Loads and initializes a single plugin from its directory, registering it in every runtime
        /// registry, the same way startup does for all plugins.
        /// </summary>
        internal static async Task<bool> LoadAndInitializePluginAsync(string pluginDirectory)
        {
            var metadata = PluginConfig.GetPluginMetadata(pluginDirectory);
            if (metadata == null) return false;

            // Bail out before creating an assembly load context for a plugin that is already running
            if (_allLoadedPlugins.ContainsKey(metadata.ID))
            {
                PublicApi.Instance.LogError(ClassName, $"Plugin with ID {metadata.ID} already loaded");
                return false;
            }

            var metadatas = new List<PluginMetadata> { metadata };
            Settings.UpdatePluginSettings(metadatas);

            var pair = PluginsLoader.LoadPlugin(metadata, Settings);
            if (pair?.Plugin == null) return false;

            // Since dotnet plugins need to get assembly name first, we should update plugin directory after loading plugins
            UpdatePluginDirectory(metadatas);

            if (!_allLoadedPlugins.TryAdd(metadata.ID, pair))
            {
                PublicApi.Instance.LogError(ClassName, $"Plugin with ID {metadata.ID} already loaded");
                // Clean up the just-created duplicate instance and its load context
                await DisposePluginAsync(pair);
                if (_assemblyLoaders.TryRemove(metadata.ID, out var duplicateLoader))
                {
                    pair.Plugin = null;
                    PluginAssemblyLoader.UnloadAndGetWeakReference(duplicateLoader);
                }
                return false;
            }

            return await InitializePluginAsync(pair);
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
            return [.. _externalPreviewPlugins.Values.Where(p => !PluginModified(p.Metadata.ID))];
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
            _register = register;

            var initTasks = _allLoadedPlugins.Select(x => Task.Run(() => InitializePluginAsync(x.Value)));

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

        internal static async Task<bool> InitializePluginAsync(PluginPair pair)
        {
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

                // Do not leave a plugin that failed to initialize queryable via its action keywords
                UnregisterPluginActionKeywords(pair.Metadata.ID);

                // Even if the plugin cannot be initialized, we still need to add it in all plugin list so that
                // we can remove the plugin from Plugin or Store page or Plugin Manager plugin.
                _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
                _initFailedPlugins.TryAdd(pair.Metadata.ID, pair);
                return false;
            }

            try
            {
                // Register ResultsUpdated event so that plugin query can use results updated interface
                _register?.RegisterResultsUpdatedEvent(pair);

                // Update plugin metadata translation after the plugin is initialized with IPublicAPI instance
                Internationalization.UpdatePluginMetadataTranslation(pair);

                // Add plugin to Dialog Jump plugin list after the plugin is initialized
                DialogJump.InitializeDialogJumpPlugin(pair);

                // Add plugin to lists after the plugin is initialized
                AddPluginToLists(pair);

                return true;
            }
            catch (Exception e)
            {
                // Roll back partial registrations so the failure surfaces as a clean init failure
                // instead of a plugin that is half-registered
                PublicApi.Instance.LogException(ClassName, $"Fail to register plugin: {pair.Metadata.Name}", e);
                RemovePluginFromLists(pair.Metadata.ID);
                UnregisterPluginActionKeywords(pair.Metadata.ID);
                _register?.UnregisterResultsUpdatedEvent(pair);
                DialogJump.RemoveDialogJumpPlugin(pair);

                pair.Metadata.Disabled = true;
                pair.Metadata.HomeDisabled = true;
                _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
                _initFailedPlugins.TryAdd(pair.Metadata.ID, pair);
                return false;
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
                        _nonGlobalPlugins.AddOrUpdate(actionKeyword,
                            _ => [pair],
                            (_, existing) =>
                            {
                                lock (existing)
                                {
                                    if (!existing.Contains(pair))
                                    {
                                        existing.Add(pair);
                                    }
                                }
                                return existing;
                            });
                        break;
                }
            }
        }

        private static void AddPluginToLists(PluginPair pair)
        {
            if (pair.Plugin is IContextMenu)
            {
                _contextMenuPlugins.TryAdd(pair.Metadata.ID, pair);
            }
            if (pair.Plugin is IAsyncHomeQuery)
            {
                _homePlugins.TryAdd(pair.Metadata.ID, pair);
            }
            if (pair.Plugin is IPluginI18n)
            {
                _translationPlugins.TryAdd(pair.Metadata.ID, pair);
            }
            if (pair.Plugin is IAsyncExternalPreview)
            {
                _externalPreviewPlugins.TryAdd(pair.Metadata.ID, pair);
            }
            _allInitializedPlugins.TryAdd(pair.Metadata.ID, pair);
        }

        private static void RemovePluginFromLists(string id)
        {
            _contextMenuPlugins.TryRemove(id, out _);
            _homePlugins.TryRemove(id, out _);
            _translationPlugins.TryRemove(id, out _);
            _externalPreviewPlugins.TryRemove(id, out _);
            _allInitializedPlugins.TryRemove(id, out _);
            _initFailedPlugins.TryRemove(id, out _);
        }

        private static void UnregisterPluginActionKeywords(string id)
        {
            _globalPlugins.TryRemove(id, out _);

            foreach (var entry in _nonGlobalPlugins.ToList())
            {
                lock (entry.Value)
                {
                    entry.Value.RemoveAll(p => p.Metadata.ID == id);

                    if (entry.Value.Count == 0)
                    {
                        _nonGlobalPlugins.TryRemove(new KeyValuePair<string, List<PluginPair>>(entry.Key, entry.Value));
                    }
                }
            }
        }

        #endregion

        #region Validate & Query Plugins

        public static ICollection<PluginPair> ValidPluginsForQuery(Query query, bool dialogJump)
        {
            if (query is null)
                return Array.Empty<PluginPair>();

            if (!TryGetNonGlobalPlugins(query.ActionKeyword, out var plugins))
            {
                if (dialogJump)
                    return [.. GetGlobalPlugins().Where(p => p.Plugin is IAsyncDialogJump && !PluginModified(p.Metadata.ID))];
                else
                    return [.. GetGlobalPlugins().Where(p => !PluginModified(p.Metadata.ID))];
            }

            var validPlugins = plugins.Where(p => !p.Metadata.Disabled && !PluginModified(p.Metadata.ID));
            if (dialogJump)
                validPlugins = validPlugins.Where(p => p.Plugin is IAsyncDialogJump);

            return [.. validPlugins];
        }

        private static bool TryGetNonGlobalPlugins(string actionKeyword, out List<PluginPair> plugins)
        {
            if (_nonGlobalPlugins.TryGetValue(actionKeyword, out var list))
            {
                lock (list)
                {
                    plugins = [.. list];
                }
                return true;
            }
            plugins = [];
            return false;
        }

        public static ICollection<PluginPair> ValidPluginsForHomeQuery()
        {
            return [.. _homePlugins.Values.Where(p => !PluginModified(p.Metadata.ID))];
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
                    AutoCompleteText = query.TrimmedQuery,
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
                    AutoCompleteText = query.TrimmedQuery,
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
                    AutoCompleteText = query.TrimmedQuery,
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

        public static Dictionary<string, List<PluginPair>> GetNonGlobalPlugins()
        {
            var nonGlobalPlugins = new Dictionary<string, List<PluginPair>>();
            foreach (var kvp in _nonGlobalPlugins)
            {
                lock (kvp.Value)
                {
                    nonGlobalPlugins.Add(kvp.Key, [.. kvp.Value]);
                }
            }
            return nonGlobalPlugins;
        }

        public static List<PluginPair> GetTranslationPlugins()
        {
            return [.. _translationPlugins.Values.Where(p => !PluginModified(p.Metadata.ID))];
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
            var pluginPair = _contextMenuPlugins.Values.Where(p => !PluginModified(p.Metadata.ID)).FirstOrDefault(o => o.Metadata.ID == result.PluginID);
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
            return _homePlugins.Values.Where(p => !PluginModified(p.Metadata.ID)).Any(p => p.Metadata.ID == id);
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

        [Obsolete("This method is only used for old Flow compatibility.")]
        public static bool ActionKeywordRegistered(string actionKeyword)
        {
            // Since now we support to assign one action keyword to multiple plugins,
            // this check is unnecessary, so we will just return false here to ensure compatibility for old plugins.
            return false;
        }

        /// <summary>
        /// used to add action keyword for multiple action keyword plugin
        /// e.g. web search
        /// </summary>
        public static void AddActionKeyword(string id, string newActionKeyword)
        {
            var plugin = GetPluginForId(id);
            if (plugin == null) return;

            if (newActionKeyword == Query.GlobalPluginWildcardSign)
            {
                _globalPlugins.TryAdd(id, plugin);
            }
            else
            {
                _nonGlobalPlugins.AddOrUpdate(newActionKeyword,
                    _ => [plugin],
                    (_, existing) =>
                    {
                        lock (existing)
                        {
                            if (!existing.Contains(plugin))
                            {
                                existing.Add(plugin);
                            }
                        }
                        return existing;
                    });
            }

            // Update action keywords and action keyword in plugin metadata
            if (!plugin.Metadata.ActionKeywords.Contains(newActionKeyword))
            {
                plugin.Metadata.ActionKeywords.Add(newActionKeyword);
            }
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
            if (plugin == null) return;

            if (oldActionkeyword == Query.GlobalPluginWildcardSign
                && // Plugins may have multiple ActionKeywords that are global, eg. WebSearch
                plugin.Metadata.ActionKeywords
                    .Count(x => x == Query.GlobalPluginWildcardSign) == 1)
            {
                _globalPlugins.TryRemove(id, out _);
            }

            if (oldActionkeyword != Query.GlobalPluginWildcardSign)
            {
                if (_nonGlobalPlugins.TryGetValue(oldActionkeyword, out var plugins))
                {
                    lock (plugins)
                    {
                        plugins.RemoveAll(p => p.Metadata.ID == id);

                        if (plugins.Count == 0)
                        {
                            _nonGlobalPlugins.TryRemove(new KeyValuePair<string, List<PluginPair>>(oldActionkeyword, plugins));
                        }
                    }
                }
            }

            // Update action keywords and action keyword in plugin metadata
            plugin.Metadata.ActionKeywords.RemoveAll(k => k == oldActionkeyword);
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

        private static bool SameOrLesserPluginVersionExists(PluginMetadata metadata)
        {
            if (!Version.TryParse(metadata.Version, out var newVersion))
                return true; // If version is not valid, we assume it is lesser than any existing version

            // Get all plugins even if initialization failed so that we can check if the plugin with the same ID exists
            return GetAllInitializedPlugins(includeFailed: true).Any(x => x.Metadata.ID == metadata.ID
                && Version.TryParse(x.Metadata.Version, out var version)
                && newVersion <= version);
        }

        #endregion

        #region Public Functions

        public static bool PluginModified(string id)
        {
            return ModifiedPlugins.ContainsKey(id);
        }

        internal static void ClearPluginModified(string id)
        {
            ModifiedPlugins.TryRemove(id, out _);
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

            ModifiedPlugins.TryAdd(existingVersion.ID, 0);
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

            try
            {
                if (!plugin.IsFromLocalInstallPath)
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

                PluginMetadata newMetadata;
                try
                {
                    newMetadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(metadataJsonFilePath)) ??
                        throw new JsonException("Deserialized metadata is null");
                }
                catch (Exception ex)
                {
                    PublicApi.Instance.ShowMsgError(Localize.failedToInstallPluginTitle(plugin.Name),
                        Localize.pluginJsonInvalidOrCorrupted());
                    PublicApi.Instance.LogException(ClassName,
                        $"Failed to deserialize plugin metadata for plugin {plugin.Name} from file {metadataJsonFilePath}", ex);
                    return false;
                }

                if (!string.Equals(newMetadata.ID, plugin.ID, StringComparison.Ordinal))
                {
                    // A mismatched package would install and later load under a different identity
                    // than the plugin the user asked for
                    PublicApi.Instance.ShowMsgError(Localize.failedToInstallPluginTitle(plugin.Name),
                        Localize.pluginIDMismatchMessage());
                    PublicApi.Instance.LogError(ClassName,
                        $"Plugin package ID <{newMetadata.ID}> does not match the requested plugin ID <{plugin.ID}> for {plugin.Name}");
                    return false;
                }

                if (SameOrLesserPluginVersionExists(newMetadata))
                {
                    PublicApi.Instance.ShowMsgError(Localize.failedToInstallPluginTitle(plugin.Name),
                        Localize.pluginExistAlreadyMessage());
                    return false;
                }

                if (!IsMinimumAppVersionSatisfied(newMetadata.Name, newMetadata.MinimumAppVersion))
                {
                    // Ask users if they want to install the plugin that doesn't satisfy the minimum app version requirement
                    if (PublicApi.Instance.ShowMsgBox(
                        Localize.pluginMinimumAppVersionUnsatisfiedMessage(newMetadata.Name, Environment.NewLine),
                        Localize.pluginMinimumAppVersionUnsatisfiedTitle(newMetadata.Name, newMetadata.MinimumAppVersion),
                        MessageBoxButton.YesNo) == MessageBoxResult.No)
                    {
                        return false;
                    }
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

                // Check if marker file exists and delete it
                try
                {
                    var markerFilePath = Path.Combine(newPluginPath, DataLocation.PluginDeleteFile);
                    if (File.Exists(markerFilePath))
                        File.Delete(markerFilePath);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Failed to delete plugin marker file in {newPluginPath}", e);
                }

                if (checkModified)
                {
                    ModifiedPlugins.TryAdd(plugin.ID, 0);
                }

                // Remember where this version was installed so a hot reload can load it without a restart
                _pendingInstallPaths[plugin.ID] = newPluginPath;

                return true;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempFolderPluginPath))
                        Directory.Delete(tempFolderPluginPath, true);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Failed to delete temp folder {tempFolderPluginPath}", e);
                }
            }
        }

        internal static async Task<bool> UninstallPluginAsync(PluginMetadata plugin, bool removePluginFromSettings, bool removePluginSettings, bool checkModified)
        {
            // Take the same per-plugin lifecycle lock as ReloadPluginAsync so a concurrent reload
            // cannot resurrect a plugin that is being uninstalled or race its file removal
            var semaphore = _reloadLocks.GetOrAdd(plugin.ID, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                return await UninstallPluginUnlockedAsync(plugin, removePluginFromSettings, removePluginSettings, checkModified);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static async Task<bool> UninstallPluginUnlockedAsync(PluginMetadata plugin, bool removePluginFromSettings, bool removePluginSettings, bool checkModified)
        {
            if (checkModified && PluginModified(plugin.ID))
            {
                PublicApi.Instance.ShowMsgError(Localize.pluginModifiedAlreadyTitle(plugin.Name),
                    Localize.pluginModifiedAlreadyMessage());
                return false;
            }

            // Fully unloaded plugins release all file handles, so their directory can be deleted
            // immediately instead of being marked for deletion on the next startup
            var fullyUnloaded = false;

            if (removePluginSettings || removePluginFromSettings)
            {
                // If we want to remove plugin from AllPlugins,
                // we need to dispose them so that they can release file handles
                // which can help FL to delete the plugin settings & cache folders successfully
                var pluginPairs = GetAllInitializedPlugins(includeFailed: true).Where(p => p.Metadata.ID == plugin.ID).ToList();
                foreach (var pluginPair in pluginPairs)
                {
                    if (removePluginFromSettings && HotReloadEnabled)
                    {
                        fullyUnloaded = await UnloadPluginAsync(pluginPair);
                    }
                    else
                    {
                        await DisposePluginAsync(pluginPair);
                    }
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
                _allLoadedPlugins.TryRemove(plugin.ID, out var _);
                RemovePluginFromLists(plugin.ID);
                UnregisterPluginActionKeywords(plugin.ID);
            }

            // When the plugin was fully unloaded, its directory can be removed right away and
            // no restart is needed; otherwise mark it for deletion on next startup
            var deleted = false;
            if (fullyUnloaded)
            {
                try
                {
                    deleted = FilesFolders.TryDeleteDirectoryRobust(plugin.PluginDirectory);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName,
                        $"Failed to delete plugin folder for {plugin.Name}, marking it for deletion on next startup", e);
                }
            }

            if (!deleted)
            {
                // Marked for deletion. Will be deleted on next start up
                using var _ = File.CreateText(Path.Combine(plugin.PluginDirectory, DataLocation.PluginDeleteFile));

                if (checkModified)
                {
                    ModifiedPlugins.TryAdd(plugin.ID, 0);
                }
            }

            return true;
        }

        internal static bool IsMinimumAppVersionSatisfied(string pluginName, string minimumAppVersion)
        {
            // If the minimum app version is not specified in plugin.json, this plugin is compatible with all app versions
            if (string.IsNullOrEmpty(minimumAppVersion))
                return true;

            var appVersion = Version.Parse(Constant.Version);

            if (!Version.TryParse(minimumAppVersion, out var minimumVersion))
            {
                PublicApi.Instance.LogError(ClassName,
                    $"Failed to parse the minimum app version {minimumAppVersion} for plugin {pluginName}.");
                return false;  // If the minimum app version specified in plugin.json is invalid, we assume it is not satisfied
            }

            if (appVersion >= minimumVersion)
                return true;

            return false;
        }

        #endregion

        #endregion
    }
}
