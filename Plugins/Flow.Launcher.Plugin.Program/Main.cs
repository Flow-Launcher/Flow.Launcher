using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Views;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Extensions.Caching.Memory;
using Path = System.IO.Path;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher.Plugin.Program
{
    public class Main : ISettingProvider, IAsyncPlugin, IPluginI18n, IContextMenu, IAsyncReloadable, IDisposable
    {
        private const string Win32CacheName = "Win32";
        private const string UwpCacheName = "UWP";

        internal static List<Win32> _win32s { get; private set; }
        internal static List<UWPApp> _uwps { get; private set; }
        internal static Settings _settings { get; private set; }

        internal static SemaphoreSlim _win32sLock = new(1, 1);
        internal static SemaphoreSlim _uwpsLock = new(1, 1);

        internal static PluginInitContext Context { get; private set; }

        private static readonly List<Result> emptyResults = new();

        private static readonly MemoryCacheOptions cacheOptions = new() { SizeLimit = 1560 };
        private static MemoryCache cache = new(cacheOptions);

        private static readonly string[] commonUninstallerNames =
        {
            "uninst.exe",
            "unins000.exe",
            "uninst000.exe",
            "uninstall.exe"
        };
        private static readonly string[] commonUninstallerPrefixs =
        {
            "uninstall",//en
            "卸载",//zh-cn
            "卸載",//zh-tw
            "видалити",//uk-UA
            "удалить",//ru
            "désinstaller",//fr
            "アンインストール",//ja
            "deïnstalleren",//nl
            "odinstaluj",//pl
            "afinstallere",//da
            "deinstallieren",//de
            "삭제",//ko
            "деинсталирај",//sr
            "desinstalar",//pt-pt
            "desinstalar",//pt-br
            "desinstalar",//es
            "desinstalar",//es-419
            "disinstallare",//it
            "avinstallere",//nb-NO
            "odinštalovať",//sk
            "kaldır",//tr
            "odinstalovat",//cs
            "إلغاء التثبيت",//ar
            "gỡ bỏ",//vi-vn
            "הסרה"//he
        };
        private const string ExeUninstallerSuffix = ".exe";
        private const string InkUninstallerSuffix = ".lnk";

        private static readonly string WindowsAppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");

        static Main()
        {
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var result = await cache.GetOrCreateAsync(query.Search, async entry =>
            {
                var resultList = await Task.Run(async () =>
                {
                    await _win32sLock.WaitAsync(token);
                    await _uwpsLock.WaitAsync(token);

                    try
                    {
                        // Collect all UWP Windows app directories
                        var uwpsDirectories = _settings.HideDuplicatedWindowsApp ? _uwps
                            .Where(uwp => !string.IsNullOrEmpty(uwp.Location)) // Exclude invalid paths
                            .Where(uwp => uwp.Location.StartsWith(WindowsAppPath, StringComparison.OrdinalIgnoreCase)) // Keep system apps
                            .Select(uwp => uwp.Location.TrimEnd('\\')) // Remove trailing slash
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray() : null;

                        return _win32s.Cast<IProgram>()
                                .Concat(_uwps)
                                .AsParallel()
                                .WithCancellation(token)
                                .Where(HideUninstallersFilter)
                                .Where(p => HideDuplicatedWindowsAppFilter(p, uwpsDirectories))
                                .Where(p => p.Enabled)
                                .Select(p => p.Result(query.Search, Context.API))
                                .Where(r => r?.Score > 0)
                                .ToList();
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Debug("|Flow.Launcher.Plugin.Program.Main|Query operation cancelled");
                        return emptyResults;
                    }
                    finally
                    {
                        _uwpsLock.Release();
                        _win32sLock.Release();
                    }
                }, token);

                resultList = resultList.Any() ? resultList : emptyResults;

                entry.SetSize(resultList.Count);
                entry.SetSlidingExpiration(TimeSpan.FromHours(8));

                return resultList;
            });

            return result;
        }

        private bool HideUninstallersFilter(IProgram program)
        {
            if (!_settings.HideUninstallers) return true;
            if (program is not Win32 win32) return true;

            // First check the executable path
            var fileName = Path.GetFileName(win32.ExecutablePath);
            // For cases when the uninstaller is named like "uninst.exe"
            if (commonUninstallerNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)) return false;
            // For cases when the uninstaller is named like "Uninstall Program Name.exe"
            foreach (var prefix in commonUninstallerPrefixs)
            {
                if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    fileName.EndsWith(ExeUninstallerSuffix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Second check the lnk path
            if (!string.IsNullOrEmpty(win32.LnkResolvedPath))
            {
                var inkFileName = Path.GetFileName(win32.FullPath);
                // For cases when the uninstaller is named like "Uninstall Program Name.ink"
                foreach (var prefix in commonUninstallerPrefixs)
                {
                    if (inkFileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        inkFileName.EndsWith(InkUninstallerSuffix, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            return true;
        }

        private static bool HideDuplicatedWindowsAppFilter(IProgram program, string[] uwpsDirectories)
        {
            if (uwpsDirectories == null || uwpsDirectories.Length == 0) return true;
            if (program is UWPApp) return true;

            var location = program.Location.TrimEnd('\\'); // Ensure trailing slash
            if (string.IsNullOrEmpty(location))
                return true; // Keep if location is invalid

            if (!location.StartsWith(WindowsAppPath, StringComparison.OrdinalIgnoreCase))
                return true; // Keep if not a Windows app

            // Check if the any Win32 executable directory contains UWP Windows app location matches 
            return !uwpsDirectories.Any(uwpDirectory =>
                location.StartsWith(uwpDirectory, StringComparison.OrdinalIgnoreCase));
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            await Stopwatch.NormalAsync("|Flow.Launcher.Plugin.Program.Main|Preload programs cost", async () =>
            {
                var pluginCachePath = Context.CurrentPluginMetadata.PluginCacheDirectoryPath;

                FilesFolders.ValidateDirectory(pluginCachePath);

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

                // Move old cache files to the new cache directory
                var oldWin32CacheFile = Path.Combine(DataLocation.CacheDirectory, $"{Win32CacheName}.cache");
                var newWin32CacheFile = Path.Combine(pluginCachePath, $"{Win32CacheName}.cache");
                MoveFile(oldWin32CacheFile, newWin32CacheFile);
                var oldUWPCacheFile = Path.Combine(DataLocation.CacheDirectory, $"{UwpCacheName}.cache");
                var newUWPCacheFile = Path.Combine(pluginCachePath, $"{UwpCacheName}.cache");
                MoveFile(oldUWPCacheFile, newUWPCacheFile);

                await _win32sLock.WaitAsync();
                _win32s = await context.API.LoadCacheBinaryStorageAsync(Win32CacheName, pluginCachePath, new List<Win32>());
                _win32sLock.Release();

                await _uwpsLock.WaitAsync();
                _uwps = await context.API.LoadCacheBinaryStorageAsync(UwpCacheName, pluginCachePath, new List<UWPApp>());
                _uwpsLock.Release();
            });
            await _win32sLock.WaitAsync();
            await _uwpsLock.WaitAsync();

            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Count}>");
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload uwps <{_uwps.Count}>");

            bool cacheEmpty = !_win32s.Any() || !_uwps.Any();

            _win32sLock.Release();
            _uwpsLock.Release();

            if (cacheEmpty || _settings.LastIndexTime.AddHours(30) < DateTime.Now)
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

            static void WatchProgramUpdate()
            {
                Win32.WatchProgramUpdate(_settings);
                _ = UWPPackage.WatchPackageChange();
            }
        }

        public static async Task IndexWin32ProgramsAsync()
        {
            var win32S = Win32.All(_settings);
            await _win32sLock.WaitAsync();
            _win32s.Clear();
            foreach (var win32 in win32S)
            {
                _win32s.Add(win32);
            }
            _win32sLock.Release();
            ResetCache();
            await Context.API.SaveCacheBinaryStorageAsync<List<Win32>>(Win32CacheName, Context.CurrentPluginMetadata.PluginCacheDirectoryPath);
            _settings.LastIndexTime = DateTime.Now;
        }

        public static async Task IndexUwpProgramsAsync()
        {
            var uwps = UWPPackage.All(_settings);
            await _uwpsLock.WaitAsync();
            _uwps.Clear();
            foreach (var uwp in uwps)
            {
                _uwps.Add(uwp);
            }
            _uwpsLock.Release();
            ResetCache();
            await Context.API.SaveCacheBinaryStorageAsync<List<UWPApp>>(UwpCacheName, Context.CurrentPluginMetadata.PluginCacheDirectoryPath);
            _settings.LastIndexTime = DateTime.Now;
        }

        public static async Task IndexProgramsAsync()
        {
            var win32Task = Task.Run(async () =>
            {
                await Stopwatch.NormalAsync("|Flow.Launcher.Plugin.Program.Main|Win32Program index cost", IndexWin32ProgramsAsync);
            });

            var uwpTask = Task.Run(async () =>
            {
                await Stopwatch.NormalAsync("|Flow.Launcher.Plugin.Program.Main|UWPProgram index cost", IndexUwpProgramsAsync);
            });

            await Task.WhenAll(win32Task, uwpTask).ConfigureAwait(false);
        }

        internal static void ResetCache()
        {
            var oldCache = cache;
            cache = new MemoryCache(cacheOptions);
            oldCache.Dispose();
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(Context, _settings);
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
                        DisableProgram(program);
                        Context.API.ShowMsg(
                            Context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_success"),
                            Context.API.GetTranslation(
                                "flowlauncher_plugin_program_disable_dlgtitle_success_message"));
                        Context.API.ReQuery();
                        return false;
                    },
                    IcoPath = "Images/disable.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xece4"),
                }
            );

            return menuOptions;
        }

        private static async Task DisableProgram(IProgram programToDelete)
        {
            if (_settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                return;

            await _uwpsLock.WaitAsync();
            if (_uwps.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                var program = _uwps.First(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                program.Enabled = false;
                _settings.DisabledProgramSources.Add(new ProgramSource(program));
                _uwpsLock.Release();

                // Reindex UWP programs
                _ = Task.Run(() =>
                {
                    _ = IndexUwpProgramsAsync();
                });
                return;
            }
            
            await _win32sLock.WaitAsync();
            if (_win32s.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                var program = _win32s.First(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                program.Enabled = false;
                _settings.DisabledProgramSources.Add(new ProgramSource(program));
                _win32sLock.Release();

                // Reindex Win32 programs
                _ = Task.Run(() =>
                {
                    _ = IndexWin32ProgramsAsync();
                });
            }
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
                Context.API.ShowMsg(title, string.Format(message, info.FileName), string.Empty);
            }
        }

        public async Task ReloadDataAsync()
        {
            await IndexProgramsAsync();
        }

        public void Dispose()
        {
            Win32.Dispose();
        }
    }
}
