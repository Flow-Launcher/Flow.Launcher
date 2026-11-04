#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal sealed class PythonDependencyInstaller
    {
        private const string SitePackagesName = "site-packages";
        private const string PythonVersionProbe = "import sys, sysconfig; print(sys.version.split()[0], sys.implementation.name, sysconfig.get_platform(), sysconfig.get_config_var('EXT_SUFFIX') or '')";
        private const int ErrorLinesInMessage = 5;

        private static readonly IReadOnlyDictionary<string, string> NoEnvironment = new Dictionary<string, string>();
        private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes(15);

        private readonly PluginsSettings _settings;
        private readonly UvLocator _uvLocator;
        private readonly IProcessRunner _runner;
        private readonly string _dependenciesRoot;
        private readonly bool _portable;
        private readonly string _uvCachePath;
        private readonly Action<string> _logError;
        private readonly Action<PluginMetadata> _onInstalling;
        private readonly TimeSpan _processTimeout;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _pluginLocks = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningInstalls = new();
        private readonly ConcurrentDictionary<string, string> _pythonVersions = new(StringComparer.OrdinalIgnoreCase);

        public PythonDependencyInstaller(
            PluginsSettings settings,
            UvLocator uvLocator,
            IProcessRunner runner,
            string dependenciesRoot,
            bool portable,
            string uvCachePath,
            Action<string>? logError = null,
            Action<PluginMetadata>? onInstalling = null,
            TimeSpan? processTimeout = null)
        {
            _settings = settings;
            _uvLocator = uvLocator;
            _runner = runner;
            _dependenciesRoot = dependenciesRoot;
            _portable = portable;
            _uvCachePath = uvCachePath;
            _logError = logError ?? (_ => { });
            _onInstalling = onInstalling ?? (_ => { });
            _processTimeout = processTimeout ?? DefaultProcessTimeout;
        }

        internal UvLocator UvLocator => _uvLocator;

        public static PythonDependencyInstaller CreateDefault(PluginsSettings settings)
        {
            return new PythonDependencyInstaller(
                settings,
                UvLocator.CreateDefault(settings),
                new ProcessRunner(),
                DataLocation.PythonDependenciesPath,
                DataLocation.PortableDataLocationInUse(),
                DataLocation.UvCachePath,
                message => PublicApi.Instance.LogError(nameof(PythonDependencyInstaller), message),
                metadata => PublicApi.Instance.ShowMsg(
                    DependencyMessages.InstallingTitle(),
                    DependencyMessages.InstallingMessage(metadata.Name)));
        }

        public async Task<string?> EnsureInstalledAsync(PluginMetadata metadata, string pythonExecutable, CancellationToken token)
        {
            var dependencies = DependencyRequirements.Normalize(metadata.Dependencies ?? new List<string>());
            if (dependencies.Count == 0)
            {
                return null;
            }

            if (!IsUsableAsFolderName(metadata.ID))
            {
                _logError($"Plugin {metadata.Name} has the ID '{metadata.ID}', which cannot be used as a folder name");
                throw new PythonDependencyException(DependencyMessages.InvalidPluginId(metadata.Name));
            }

            if (!_settings.AllowUnpinnedPythonDependencies)
            {
                var unpinned = DependencyRequirements.FindUnpinned(dependencies);
                if (unpinned.Count > 0)
                {
                    throw new PythonDependencyException(DependencyMessages.NotPinned(metadata.Name, string.Join(", ", unpinned)));
                }
            }

            var pluginLock = LockFor(metadata.ID);
            await pluginLock.WaitAsync(token).ConfigureAwait(false);
            using var install = CancellationTokenSource.CreateLinkedTokenSource(token);
            _runningInstalls[metadata.ID] = install;
            try
            {
                return await EnsureInstalledLockedAsync(metadata, dependencies, pythonExecutable, install.Token).ConfigureAwait(false);
            }
            finally
            {
                _runningInstalls.TryRemove(new KeyValuePair<string, CancellationTokenSource>(metadata.ID, install));
                pluginLock.Release();
            }
        }

        public async Task CleanupStaleInstallsAsync(string pluginId)
        {
            if (!IsUsableAsFolderName(pluginId))
            {
                _logError($"Skipped cleaning up Python dependencies of plugin ID '{pluginId}' because it cannot be used as a folder name");
                return;
            }

            var pluginLock = LockFor(pluginId);
            await pluginLock.WaitAsync().ConfigureAwait(false);
            try
            {
                DeleteStaleInstalls(pluginId);
            }
            finally
            {
                pluginLock.Release();
            }
        }

        private void DeleteStaleInstalls(string pluginId)
        {
            try
            {
                var pluginRoot = Path.Combine(_dependenciesRoot, pluginId);
                var marker = DependencyInstallMarker.Load(Path.Combine(pluginRoot, DependencyInstallMarker.FileName));
                if (marker is null || !Directory.Exists(pluginRoot))
                {
                    return;
                }

                foreach (var directory in Directory.EnumerateDirectories(pluginRoot))
                {
                    if (!string.Equals(Path.GetFileName(directory), marker.InstallFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteDirectory(directory);
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logError($"Failed to clean up old Python dependencies of plugin {pluginId}: {e.Message}");
            }
        }

        /// <summary>
        /// Deletes all dependencies of the plugin. An install still running for it, such as the one started
        /// after a store install, is cancelled first so it cannot write the folder back.
        /// </summary>
        public async Task DeleteDependenciesAsync(string pluginId)
        {
            if (!IsUsableAsFolderName(pluginId))
            {
                _logError($"Skipped removing Python dependencies of plugin ID '{pluginId}' because it cannot be used as a folder name");
                return;
            }

            if (_runningInstalls.TryGetValue(pluginId, out var running))
            {
                TryCancel(running);
            }

            var pluginLock = LockFor(pluginId);
            await pluginLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var pluginRoot = Path.Combine(_dependenciesRoot, pluginId);
                if (Directory.Exists(pluginRoot))
                {
                    Directory.Delete(pluginRoot, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logError($"Failed to remove Python dependencies of plugin {pluginId}: {e.Message}");
            }
            finally
            {
                pluginLock.Release();
            }
        }

        /// <summary>
        /// Deletes dependency folders of plugins that are no longer installed, for example when the folder
        /// was still in use during the uninstall. Call it before any install can start.
        /// </summary>
        public void RemoveOrphanedDependencies(IReadOnlyCollection<string> installedPluginIds)
        {
            // An empty list more likely means the plugins could not be read than that none are installed.
            if (installedPluginIds.Count == 0 || !Directory.Exists(_dependenciesRoot))
            {
                return;
            }

            var installed = new HashSet<string>(installedPluginIds, StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(_dependenciesRoot).ToList())
                {
                    var pluginId = Path.GetFileName(directory);
                    if (installed.Contains(pluginId))
                    {
                        continue;
                    }

                    try
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        _logError($"Failed to remove Python dependencies of uninstalled plugin {pluginId}: {e.Message}");
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logError($"Failed to look for Python dependencies of uninstalled plugins: {e.Message}");
            }
        }

        private SemaphoreSlim LockFor(string pluginId)
        {
            return _pluginLocks.GetOrAdd(pluginId, _ => new SemaphoreSlim(1, 1));
        }

        private static void TryCancel(CancellationTokenSource source)
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The install finished in the meantime.
            }
        }

        public static string ToConsolePython(string pythonExecutable)
        {
            if (!string.Equals(Path.GetFileName(pythonExecutable), "pythonw.exe", StringComparison.OrdinalIgnoreCase))
            {
                return pythonExecutable;
            }

            var sibling = Path.Combine(Path.GetDirectoryName(pythonExecutable) ?? string.Empty, "python.exe");
            return File.Exists(sibling) ? sibling : pythonExecutable;
        }

        // Windows drops trailing dots and spaces from a folder name, which also rules out "." and "..".
        private static bool IsUsableAsFolderName(string? pluginId)
        {
            return !string.IsNullOrWhiteSpace(pluginId)
                && pluginId == pluginId.Trim()
                && !pluginId.EndsWith('.')
                && pluginId.IndexOfAny(new[] { '/', '\\' }) < 0
                && pluginId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && !Path.IsPathRooted(pluginId);
        }

        private async Task<string> EnsureInstalledLockedAsync(
            PluginMetadata metadata,
            IReadOnlyList<string> dependencies,
            string pythonExecutable,
            CancellationToken token)
        {
            var python = ToConsolePython(FilesFolders.ResolveAbsolutePath(pythonExecutable));
            var pythonVersion = await GetPythonVersionAsync(metadata, python, token).ConfigureAwait(false);
            var installKey = DependencyInstallMarker.ComputeKey(dependencies, python, pythonVersion);

            var pluginRoot = Path.Combine(_dependenciesRoot, metadata.ID);
            var markerPath = Path.Combine(pluginRoot, DependencyInstallMarker.FileName);

            var marker = DependencyInstallMarker.Load(markerPath);
            if (marker?.Matches(installKey) == true)
            {
                var installedSitePackages = Path.Combine(pluginRoot, marker.InstallFolder, SitePackagesName);
                if (Directory.Exists(installedSitePackages))
                {
                    return installedSitePackages;
                }
            }

            var uvPath = await LocateUvAsync(metadata, token).ConfigureAwait(false);
            var installFolder = CreateInstallFolderName(metadata, pluginRoot, installKey);
            var installDirectory = Path.Combine(pluginRoot, installFolder);
            var sitePackages = Path.Combine(installDirectory, SitePackagesName);
            NotifyInstalling(metadata);

            try
            {
                var install = await RunAsync(metadata, uvPath, BuildInstallArguments(sitePackages, python, dependencies), token)
                    .ConfigureAwait(false);
                if (install.ExitCode != 0)
                {
                    _logError($"uv failed to install dependencies of plugin {metadata.Name} (exit code {install.ExitCode}). StdOut: {install.StdOut} StdErr: {install.StdErr}");
                    throw new PythonDependencyException(DependencyMessages.InstallFailed(metadata.Name, LastErrorLines(install.StdErr)));
                }

                var resolved = await FreezeAsync(metadata, uvPath, sitePackages, python, token).ConfigureAwait(false);
                var uvVersion = await GetUvVersionAsync(metadata, uvPath, token).ConfigureAwait(false);
                SaveMarker(
                    metadata,
                    markerPath,
                    new DependencyInstallMarker(installKey, dependencies, python, pythonVersion, uvVersion, resolved, installFolder));
            }
            catch (Exception)
            {
                TryDeleteDirectory(installDirectory);
                throw;
            }

            return sitePackages;
        }

        private void NotifyInstalling(PluginMetadata metadata)
        {
            if (metadata.Disabled && metadata.HomeDisabled)
            {
                return;
            }

            try
            {
                _onInstalling(metadata);
            }
            catch (Exception e)
            {
                _logError($"Could not show the installing notice for plugin {metadata.Name}: {e}");
            }
        }

        private async Task<string> GetPythonVersionAsync(PluginMetadata metadata, string python, CancellationToken token)
        {
            var cacheKey = VersionCacheKey(python);
            if (_pythonVersions.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var result = await RunAsync(metadata, python, new[] { "-c", PythonVersionProbe }, token, useUvEnvironment: false)
                .ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                _logError($"Could not determine the version of Python at {python} (exit code {result.ExitCode}). StdErr: {result.StdErr}");
                throw new PythonDependencyException(DependencyMessages.InstallFailed(metadata.Name, LastErrorLines(result.StdErr)));
            }

            return _pythonVersions[cacheKey] = result.StdOut.Trim();
        }

        // A Python upgraded in place keeps its path, but its executable gets a new timestamp.
        private static string VersionCacheKey(string python)
        {
            try
            {
                return $"{python}|{File.GetLastWriteTimeUtc(python).Ticks}";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return python;
            }
        }

        private async Task<string> LocateUvAsync(PluginMetadata metadata, CancellationToken token)
        {
            try
            {
                return await _uvLocator.GetUvPathAsync(token).ConfigureAwait(false);
            }
            catch (Exception e) when (!IsCallerCancellation(e, token))
            {
                _logError($"Could not locate uv for plugin {metadata.Name}: {e}");
                throw new PythonDependencyException(DependencyMessages.UvUnavailable(metadata.Name), e);
            }
        }

        private async Task<IReadOnlyList<string>> FreezeAsync(
            PluginMetadata metadata, string uvPath, string sitePackages, string python, CancellationToken token)
        {
            var arguments = new[] { "pip", "freeze", "--target", sitePackages, "--python", python };
            var result = await RunAsync(metadata, uvPath, arguments, token).ConfigureAwait(false);
            return result.ExitCode == 0 ? NonBlankLines(result.StdOut) : Array.Empty<string>();
        }

        private async Task<string> GetUvVersionAsync(PluginMetadata metadata, string uvPath, CancellationToken token)
        {
            var result = await RunAsync(metadata, uvPath, new[] { "--version" }, token).ConfigureAwait(false);
            return result.ExitCode == 0 ? result.StdOut.Trim() : string.Empty;
        }

        private async Task<ProcessResult> RunAsync(
            PluginMetadata metadata,
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken token,
            bool useUvEnvironment = true)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(_processTimeout);
            try
            {
                return await _runner.RunAsync(fileName, arguments, useUvEnvironment ? UvEnvironment.Build(_portable, _uvCachePath) : NoEnvironment, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (timeout.IsCancellationRequested && !token.IsCancellationRequested)
            {
                _logError($"{fileName} did not finish within {_processTimeout} for plugin {metadata.Name}: {e.Message}");
                throw new PythonDependencyException(DependencyMessages.InstallFailed(metadata.Name, string.Empty), e);
            }
            catch (Exception e) when (e is not PythonDependencyException && !IsCallerCancellation(e, token))
            {
                _logError($"Failed to run {fileName} for plugin {metadata.Name}: {e}");
                throw new PythonDependencyException(DependencyMessages.InstallFailed(metadata.Name, e.Message), e);
            }
        }

        // HttpClient timeouts surface as TaskCanceledException while the caller's token is untouched.
        private static bool IsCallerCancellation(Exception exception, CancellationToken token)
        {
            return exception is OperationCanceledException && token.IsCancellationRequested;
        }

        private static IReadOnlyList<string> BuildInstallArguments(string sitePackages, string python, IReadOnlyList<string> dependencies)
        {
            var arguments = new List<string> { "pip", "install", "--no-config", "--target", sitePackages, "--python", python };
            arguments.AddRange(dependencies);
            return arguments;
        }

        // A uv left running by an earlier Flow process may still write to an older folder, so every attempt gets a fresh one.
        private string CreateInstallFolderName(PluginMetadata metadata, string pluginRoot, string installKey)
        {
            try
            {
                Directory.CreateDirectory(pluginRoot);
                string installFolder;
                do
                {
                    installFolder = DependencyInstallMarker.NewInstallFolder(installKey);
                }
                while (Directory.Exists(Path.Combine(pluginRoot, installFolder)));

                return installFolder;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logError($"Could not prepare {pluginRoot} for plugin {metadata.Name}: {e}");
                throw new PythonDependencyException(DependencyMessages.InstallFailed(metadata.Name, e.Message), e);
            }
        }

        private void SaveMarker(PluginMetadata metadata, string markerPath, DependencyInstallMarker marker)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
                marker.Save(markerPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logError($"Could not save the install marker for plugin {metadata.Name}: {e}");
                throw new PythonDependencyException(DependencyMessages.InstallFailed(metadata.Name, e.Message), e);
            }
        }

        private static string LastErrorLines(string stdErr)
        {
            return string.Join(Environment.NewLine, NonBlankLines(stdErr).TakeLast(ErrorLinesInMessage));
        }

        private static IReadOnlyList<string> NonBlankLines(string text)
        {
            return text
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A leftover folder is harmless and is retried on the next cleanup.
            }
        }
    }
}
