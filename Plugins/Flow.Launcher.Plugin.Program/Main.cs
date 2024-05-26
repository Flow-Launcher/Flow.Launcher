using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Views;
using Flow.Launcher.Plugin.Program.Views.Models;
using Microsoft.Extensions.Caching.Memory;
using Path = System.IO.Path;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher.Plugin.Program
{
    public class Main : ISettingProvider, IAsyncPlugin, IPluginI18n, IContextMenu, ISavable, IAsyncReloadable,
        IDisposable
    {
        internal static Win32[] _win32s { get; set; }
        internal static UWPApp[] _uwps { get; set; }
        internal static Settings _settings { get; set; }


        internal static PluginInitContext Context { get; private set; }

        private static BinaryStorage<Win32[]> _win32Storage;
        private static BinaryStorage<UWPApp[]> _uwpStorage;

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
        // For cases when the uninstaller is named like "Uninstall Program Name.exe"
        private const string CommonUninstallerPrefix = "uninstall";
        private const string CommonUninstallerSuffix = ".exe";

        static Main()
        {
        }

        public void Save()
        {
            _win32Storage.SaveAsync(_win32s);
            _uwpStorage.SaveAsync(_uwps);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var result = await cache.GetOrCreateAsync(query.Search, async entry =>
            {
                var resultList = await Task.Run(() =>
                    _win32s.Cast<IProgram>()
                        .Concat(_uwps)
                        .AsParallel()
                        .WithCancellation(token)
                        .Where(HideUninstallersFilter)
                        .Where(p => p.Enabled)
                        .Select(p => p.Result(query.Search, Context.API))
                        .Where(r => r?.Score > 0)
                        .ToList());

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
            var fileName = Path.GetFileName(win32.ExecutablePath);
            return !commonUninstallerNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) &&
                   !(fileName.StartsWith(CommonUninstallerPrefix, StringComparison.OrdinalIgnoreCase) &&
                     fileName.EndsWith(CommonUninstallerSuffix, StringComparison.OrdinalIgnoreCase));
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            await Stopwatch.NormalAsync("|Flow.Launcher.Plugin.Program.Main|Preload programs cost", async () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = await _win32Storage.TryLoadAsync(Array.Empty<Win32>());
                _uwpStorage = new BinaryStorage<UWPApp[]>("UWP");
                _uwps = await _uwpStorage.TryLoadAsync(Array.Empty<UWPApp>());
            });
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");

            bool cacheEmpty = !_win32s.Any() || !_uwps.Any();

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

        public static void IndexWin32Programs()
        {
            var win32S = Win32.All(_settings);
            _win32s = win32S;
            ResetCache();
            _win32Storage.SaveAsync(_win32s);
            _settings.LastIndexTime = DateTime.Now;
        }

        public static void IndexUwpPrograms()
        {
            var applications = UWPPackage.All(_settings);
            _uwps = applications;
            ResetCache();
            _uwpStorage.SaveAsync(_uwps);
            _settings.LastIndexTime = DateTime.Now;
        }

        public static async Task IndexProgramsAsync()
        {
            var a = Task.Run(() =>
            {
                Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|UWPProgram index cost", IndexUwpPrograms);
            });
            await Task.WhenAll(a, b).ConfigureAwait(false);
        }

        internal static void ResetCache()
        {
            var oldCache = cache;
            cache = new MemoryCache(cacheOptions);
            oldCache.Dispose();
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(Context, _settings, _win32s, _uwps);
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
                        return false;
                    },
                    IcoPath = "Images/disable.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xece4"),
                }
            );

            return menuOptions;
        }

        private static void DisableProgram(IProgram programToDelete)
        {
            if (_settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                return;

            if (_uwps.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                var program = _uwps.First(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                program.Enabled = false;
                _settings.DisabledProgramSources.Add(new ProgramSource(program));
                _ = Task.Run(() =>
                {
                    IndexUwpPrograms();
                });
            }
            else if (_win32s.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                var program = _win32s.First(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                program.Enabled = false;
                _settings.DisabledProgramSources.Add(new ProgramSource(program));
                _ = Task.Run(() =>
                {
                    IndexWin32Programs();
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
