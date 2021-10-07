using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Views;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher.Plugin.Program
{
    public class Main : ISettingProvider, IAsyncPlugin, IPluginI18n, IContextMenu, ISavable, IAsyncReloadable
    {
        internal static Win32[] _win32s { get; set; }
        internal static UWP.Application[] _uwps { get; set; }
        internal static Settings _settings { get; set; }

        private static bool IsStartupIndexProgramsRequired => _settings.LastIndexTime.AddDays(3) < DateTime.Today;

        private static PluginInitContext _context;

        private static BinaryStorage<Win32[]> _win32Storage;
        private static BinaryStorage<UWP.Application[]> _uwpStorage;

        private static readonly List<Result> emptyResults = new();

        private static readonly MemoryCacheOptions cacheOptions = new()
        {
            SizeLimit = 1560
        };
        private static MemoryCache cache = new(cacheOptions);

        static Main()
        {
        }

        public void Save()
        {
            _win32Storage.Save(_win32s);
            _uwpStorage.Save(_uwps);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {

            if (IsStartupIndexProgramsRequired)
                _ = IndexPrograms();

            var result = await cache.GetOrCreateAsync(query.Search, async entry =>
             {
                 var resultList = await Task.Run(() =>
                          _win32s.Cast<IProgram>()
                          .Concat(_uwps)
                          .AsParallel()
                          .WithCancellation(token)
                          .Where(p => p.Enabled)
                          .Select(p => p.Result(query.Search, _context.API))
                          .Where(r => r?.Score > 0)
                          .ToList());

                 resultList = resultList.Any() ? resultList : emptyResults;

                 entry.SetSize(resultList.Count);
                 entry.SetSlidingExpiration(TimeSpan.FromHours(8));

                 return resultList;
             });

            return result;
        }

        public async Task InitAsync(PluginInitContext context)
        {
            _context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            await Task.Yield();

            Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Win32[] { });
                _uwpStorage = new BinaryStorage<UWP.Application[]>("UWP");
                _uwps = _uwpStorage.TryLoad(new UWP.Application[] { });
            });
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");


            bool indexedWinApps = false;
            bool indexedUWPApps = false;

            var a = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_win32s.Any())
                {
                    Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
                    indexedWinApps = true;
                }
            });

            var b = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_uwps.Any())
                {
                    Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|Win32Program index cost", IndexUwpPrograms);
                    indexedUWPApps = true;
                }
            });

            var indexTask = Task.WhenAll(a, b).ContinueWith(t =>
            {
                if (indexedWinApps && indexedUWPApps)
                    _settings.LastIndexTime = DateTime.Today;
            });

            if (!(_win32s.Any() && _uwps.Any()))
                await indexTask;
        }

        public static void IndexWin32Programs()
        {
            var win32S = Win32.All(_settings);
            _win32s = win32S;
        }

        public static void IndexUwpPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;
            var applications = support ? UWP.All() : new UWP.Application[] { };
            _uwps = applications;
        }

        public static async Task IndexPrograms()
        {
            var t1 = Task.Run(IndexWin32Programs);
            var t2 = Task.Run(IndexUwpPrograms);
            await Task.WhenAll(t1, t2).ConfigureAwait(false);
            var oldCache = cache;
            cache = new MemoryCache(cacheOptions);
            oldCache.Dispose();
            _settings.LastIndexTime = DateTime.Today;
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings, _win32s, _uwps);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_program_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menuOptions = new List<Result>();
            var program = selectedResult.ContextData as IProgram;
            if (program != null)
            {
                menuOptions = program.ContextMenus(_context.API);
            }

            menuOptions.Add(
                new Result
                {
                    Title = _context.API.GetTranslation("flowlauncher_plugin_program_disable_program"),
                    Action = c =>
                    {
                        DisableProgram(program);
                        _context.API.ShowMsg(
                            _context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_success"),
                            _context.API.GetTranslation(
                                "flowlauncher_plugin_program_disable_dlgtitle_success_message"));
                        return false;
                    },
                    IcoPath = "Images/disable.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xece4"),
                }
            );

            return menuOptions;
        }

        private void DisableProgram(IProgram programToDelete)
        {
            if (_settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                return;

            if (_uwps.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                _uwps.Where(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier)
                    .FirstOrDefault()
                    .Enabled = false;

            if (_win32s.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                _win32s.Where(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier)
                    .FirstOrDefault()
                    .Enabled = false;

            _settings.DisabledProgramSources
                .Add(
                    new Settings.DisabledProgramSource
                    {
                        Name = programToDelete.Name,
                        Location = programToDelete.Location,
                        UniqueIdentifier = programToDelete.UniqueIdentifier,
                        Enabled = false
                    }
                );
        }

        public static void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                runProcess(info);
            }
            catch (Exception)
            {
                var name = "Plugin: Program";
                var message = $"Unable to start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
            }
        }

        public async Task ReloadDataAsync()
        {
            await IndexPrograms();
        }
    }
}