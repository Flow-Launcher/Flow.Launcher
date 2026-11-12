using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Extensions.Caching.Memory;
using Path = System.IO.Path;

namespace Flow.Launcher.Plugin.Program
{
    public partial class Main : ISettingProvider, IAsyncPlugin, IPluginI18n, IContextMenu, IAsyncReloadable, IDisposable
    {
        private static readonly string ClassName = nameof(Main);

        private const string Win32CacheName = "Win32";
        private const string UwpCacheName = "UWP";

        internal static List<Win32> _win32s { get; private set; }
        internal static Settings _settings { get; private set; }

        internal static SemaphoreSlim _win32sLock = new(1, 1);

        internal static PluginInitContext Context { get; private set; }

        private static readonly Lock _lastIndexTimeLock = new();

        private static readonly List<Result> emptyResults = [];

        private static readonly MemoryCacheOptions cacheOptions = new() { SizeLimit = 1560 };
        private static MemoryCache cache = new(cacheOptions);

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            try
            {
                var result = await cache.GetOrCreateAsync(query.Search, async entry =>
                {
                    var resultList = await Task.Run(async () =>
                    {
                        // Preparing win32 programs
                        List<Win32> win32s;
                        bool win32LockAcquired = false;
                        try
                        {
                            await _win32sLock.WaitAsync(token);
                            win32LockAcquired = true;
                            win32s = [.. _win32s];
                        }
                        finally
                        {
                            // Only release the lock if it was acquired
                            if (win32LockAcquired) _win32sLock.Release();
                        }

                        // Start querying programs
                        var programs = await GetProgramsToQueryAsync(win32s, token);
                        return programs
                            .Where(p => p.Enabled)
                            .Select(p => p.Result(query.Search, Context.API))
                            .Where(r => string.IsNullOrEmpty(query.Search) || r?.Score > 0)
                            .ToList();
                    }, token);

                    resultList = resultList.Count != 0 ? resultList : emptyResults;

                    entry.SetSize(resultList.Count);
                    entry.SetSlidingExpiration(TimeSpan.FromHours(8));

                    return resultList;
                });

                return result;
            }
            catch (OperationCanceledException)
            {
                return emptyResults;
            }
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            var _win32sCount = 0;
            var uwpCacheEmpty = false;
            await Context.API.StopwatchLogInfoAsync(ClassName, "Preload programs cost", async () =>
            {
                var pluginCacheDirectory = Context.CurrentPluginMetadata.PluginCacheDirectoryPath;
                FilesFolders.ValidateDirectory(pluginCacheDirectory);

                static void MoveFile(string sourcePath, string destinationPath)
                {
                    if (!File.Exists(sourcePath))
                    {
                        return;
                    }

                    if (File.Exists(destinationPath))
                    {
                        try
                        {
                            File.Delete(sourcePath);
                        }
                        catch (Exception)
                        {
                            // Ignore, we will handle next time we start the plugin
                        }
                        return;
                    }

                    var destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDirectory) && (!string.IsNullOrEmpty(destinationDirectory)))
                    {
                        try
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }
                        catch (Exception)
                        {
                            // Ignore, we will handle next time we start the plugin
                        }
                    }
                    try
                    {
                        File.Move(sourcePath, destinationPath);
                    }
                    catch (Exception)
                    {
                        // Ignore, we will handle next time we start the plugin
                    }
                }

                // If plugin cache directory is this: D:\\Data\\Cache\\Plugins\\Flow.Launcher.Plugin.Program
                // then the parent directory is: D:\\Data\\Cache
                // So we can use the parent of the parent directory to get the cache directory path
                var directoryInfo = new DirectoryInfo(pluginCacheDirectory);
                var cacheDirectory = directoryInfo.Parent?.Parent?.FullName;
                // Move old cache files to the new cache directory if cache directory exists
                if (!string.IsNullOrEmpty(cacheDirectory))
                {
                    var oldWin32CacheFile = Path.Combine(cacheDirectory, $"{Win32CacheName}.cache");
                    var newWin32CacheFile = Path.Combine(pluginCacheDirectory, $"{Win32CacheName}.cache");
                    MoveFile(oldWin32CacheFile, newWin32CacheFile);
                    var oldUWPCacheFile = Path.Combine(cacheDirectory, $"{UwpCacheName}.cache");
                    var newUWPCacheFile = Path.Combine(pluginCacheDirectory, $"{UwpCacheName}.cache");
                    MoveFile(oldUWPCacheFile, newUWPCacheFile);
                }

                await _win32sLock.WaitAsync();
                try
                {
                    _win32s = await context.API.LoadCacheBinaryStorageAsync(Win32CacheName, pluginCacheDirectory, new List<Win32>());
                    _win32sCount = _win32s.Count;
                }
                finally
                {
                    _win32sLock.Release();
                }

                uwpCacheEmpty = await LoadUwpCacheAsync(pluginCacheDirectory);
            });
            Context.API.LogInfo(ClassName, $"Number of preload win32 programs <{_win32sCount}>");

            var cacheEmpty = _win32sCount == 0 || uwpCacheEmpty;

            bool needReindex;
            lock (_lastIndexTimeLock)
            {
                needReindex = _settings.LastIndexTime.AddHours(30) < DateTime.Now;
            }
            if (cacheEmpty || needReindex)
            {
                _ = Task.Run(async () =>
                {
                    await IndexProgramsAsync().ConfigureAwait(false);
                    WatchProgramUpdate();
                });
            }
            else
            {
                WatchProgramUpdate();
            }

            Context.API.StringMatcherBehaviorChanged += API_StringMatcherBehaviorChanged;

            static void WatchProgramUpdate()
            {
                Win32.WatchProgramUpdate(_settings);
                WatchUwpPackageChange();
            }
        }

        private void API_StringMatcherBehaviorChanged(object sender, EventArgs e)
        {
            // The cache holds results that were produced with the previous StringMatcher
            // configuration (for example: case sensitivity, diacritic handling, or scoring
            // rules). When those matcher behaviors change the cached results can become
            // stale or have incorrect scores/ordering. Reset the cache so subsequent
            // queries recompute matches using the updated matcher settings and avoid
            // returning invalid or misleading results.
            ResetCache();
        }

        public static async Task IndexWin32ProgramsAsync(bool resetCache)
        {
            await _win32sLock.WaitAsync();
            try
            {
                var win32S = Win32.All(_settings);
                _win32s.Clear();
                foreach (var win32 in win32S)
                {
                    _win32s.Add(win32);
                }
                if (resetCache)
                {
                    ResetCache();
                }
                await Context.API.SaveCacheBinaryStorageAsync<List<Win32>>(Win32CacheName, Context.CurrentPluginMetadata.PluginCacheDirectoryPath);
                lock (_lastIndexTimeLock)
                {
                    _settings.LastIndexTime = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, "Failed to index Win32 programs", e);
            }
            finally
            {
                _win32sLock.Release();
            }
        }

        public static async Task IndexProgramsAsync()
        {
            var win32Task = Task.Run(async () =>
            {
                await Context.API.StopwatchLogInfoAsync(ClassName, "Win32Program index cost", () => IndexWin32ProgramsAsync(resetCache: true));
            });

            await Task.WhenAll(win32Task, IndexUwpProgramsInBackgroundAsync()).ConfigureAwait(false);
        }

        internal static void ResetCache()
        {
            var newCache = new MemoryCache(cacheOptions);

            // Atomically swap and get the previous cache instance, avoids double-dispose/lost-assignment race
            // where each caller receives a distinct prior instance to dispose.
            var oldCache = Interlocked.Exchange(ref cache, newCache);

            // Dispose the previous instance (if any)- each caller gets a unique prior instance from above
            try
            {
                oldCache?.Dispose();
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, "Failed to dispose old program cache", e);
            }
        }

        public object CreateSettingPanelAvalonia()
        {
            return new Views.Avalonia.ProgramSetting(Context, _settings);
        }

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("flowlauncher_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("flowlauncher_plugin_program_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menuOptions = new List<Result>();
            var program = selectedResult.ContextData as IProgram;
            if (program != null)
            {
                menuOptions = program.ContextMenus(Context.API);
            }

            menuOptions.Add(
                new Result
                {
                    Title = Context.API.GetTranslation("flowlauncher_plugin_program_disable_program"),
                    Action = c =>
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var disabled = await DisableProgramAsync(program);
                                if (disabled)
                                {
                                    ResetCache();
                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_success"),
                                        Context.API.GetTranslation(
                                            "flowlauncher_plugin_program_disable_dlgtitle_success_message"));
                                }
                                Context.API.ReQuery();
                            }
                            catch (Exception e)
                            {
                                Context.API.LogException(ClassName, "Failed to disable program", e);
                            }
                        });
                        return false;
                    },
                    IcoPath = "Images/disable.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xece4"),
                }
            );

            return menuOptions;
        }

        private static async Task<bool> DisableProgramAsync(IProgram programToDelete)
        {
            if (_settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                return false;
            }

            if (await DisableUwpProgramAsync(programToDelete))
            {
                return true;
            }

            await _win32sLock.WaitAsync();
            try
            {
                var program = _win32s.FirstOrDefault(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                if (program != null)
                {
                    program.Enabled = false;
                    _settings.DisabledProgramSources.Add(new ProgramSource(program));
                    // Reindex Win32 programs
                    _ = Task.Run(() => IndexWin32ProgramsAsync(resetCache: false));
                    return true;
                }
            }
            finally
            {
                _win32sLock.Release();
            }

            return false;
        }

        public static void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                runProcess(info);
            }
            catch (Exception)
            {
                var title = Context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_error");
                var message = string.Format(Context.API.GetTranslation("flowlauncher_plugin_program_run_failed"),
                    info.FileName);
                Context.API.ShowMsgError(title, message);
            }
        }

        public async Task ReloadDataAsync()
        {
            await IndexProgramsAsync();
        }

        public void Dispose()
        {
            Context.API.StringMatcherBehaviorChanged -= API_StringMatcherBehaviorChanged;
            Win32.Dispose();
        }

        private partial Task<ParallelQuery<IProgram>> GetProgramsToQueryAsync(List<Win32> win32s, CancellationToken token);

        private static partial Task<bool> LoadUwpCacheAsync(string pluginCacheDirectory);

        static partial void WatchUwpPackageChange();

        private static partial Task IndexUwpProgramsInBackgroundAsync();

        private static partial Task<bool> DisableUwpProgramAsync(IProgram programToDelete);
    }
}
