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
                {
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
                        Context.API.ReQuery();
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
