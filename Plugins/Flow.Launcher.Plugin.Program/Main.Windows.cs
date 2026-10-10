using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Views;
using Flow.Launcher.Plugin.Program.Views.Models;
using Path = System.IO.Path;

namespace Flow.Launcher.Plugin.Program
{
    public partial class Main
    {
        internal static List<UWPApp> _uwps { get; private set; }

        internal static SemaphoreSlim _uwpsLock = new(1, 1);

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

        private async partial Task<ParallelQuery<IProgram>> GetProgramsToQueryAsync(List<Win32> win32s, CancellationToken token)
        {
            // Preparing UWP programs
            List<UWPApp> uwps;
            bool uwpsLockAcquired = false;
            try
            {
                await _uwpsLock.WaitAsync(token);
                uwpsLockAcquired = true;
                uwps = [.. _uwps];
            }
            finally
            {
                // Only release the lock if it was acquired
                if (uwpsLockAcquired) _uwpsLock.Release();
            }

            // Collect all UWP Windows app directories
            var uwpsDirectories = _settings.HideDuplicatedWindowsApp ? _uwps
                .Where(uwp => !string.IsNullOrEmpty(uwp.Location)) // Exclude invalid paths
                .Where(uwp => uwp.Location.StartsWith(WindowsAppPath, StringComparison.OrdinalIgnoreCase)) // Keep system apps
                .Select(uwp => uwp.Location.TrimEnd('\\')) // Remove trailing slash
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() : null;

            return win32s.Cast<IProgram>()
                .Concat(uwps)
                .AsParallel()
                .WithCancellation(token)
                .Where(HideUninstallersFilter)
                .Where(p => HideDuplicatedWindowsAppFilter(p, uwpsDirectories));
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

        private static async partial Task<bool> LoadUwpCacheAsync(string pluginCacheDirectory)
        {
            var _uwpsCount = 0;
            await _uwpsLock.WaitAsync();
            try
            {
                _uwps = await Context.API.LoadCacheBinaryStorageAsync(UwpCacheName, pluginCacheDirectory, new List<UWPApp>());
                _uwpsCount = _uwps.Count;
            }
            finally
            {
                _uwpsLock.Release();
            }
            Context.API.LogInfo(ClassName, $"Number of preload uwps <{_uwpsCount}>");

            return _uwpsCount == 0;
        }

        static partial void WatchUwpPackageChange()
        {
            _ = UWPPackage.WatchPackageChangeAsync();
        }

        public static async Task IndexUwpProgramsAsync(bool resetCache)
        {
            await _uwpsLock.WaitAsync();
            try
            {
                var uwps = UWPPackage.All(_settings);
                _uwps.Clear();
                foreach (var uwp in uwps)
                {
                    _uwps.Add(uwp);
                }
                if (resetCache)
                {
                    ResetCache();
                }
                await Context.API.SaveCacheBinaryStorageAsync<List<UWPApp>>(UwpCacheName, Context.CurrentPluginMetadata.PluginCacheDirectoryPath);
                lock (_lastIndexTimeLock)
                {
                    _settings.LastIndexTime = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, "Failed to index Uwp programs", e);
            }
            finally
            {
                _uwpsLock.Release();
            }
        }

        private static partial Task IndexUwpProgramsInBackgroundAsync()
        {
            return Task.Run(async () =>
            {
                await Context.API.StopwatchLogInfoAsync(ClassName, "UWPProgram index cost", () => IndexUwpProgramsAsync(resetCache: true));
            });
        }

        private static async partial Task<bool> DisableUwpProgramAsync(IProgram programToDelete)
        {
            await _uwpsLock.WaitAsync();
            try
            {
                var program = _uwps.FirstOrDefault(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                if (program != null)
                {
                    program.Enabled = false;
                    _settings.DisabledProgramSources.Add(new ProgramSource(program));
                    // Reindex UWP programs
                    _ = Task.Run(() => IndexUwpProgramsAsync(resetCache: false));
                    return true;
                }
            }
            finally
            {
                _uwpsLock.Release();
            }

            return false;
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(Context, _settings);
        }
    }
}
