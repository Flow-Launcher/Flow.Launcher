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
        private const string PythonVersionProbe = "import sys, sysconfig; print(sys.version.split()[0], sysconfig.get_platform())";
        private const int ErrorLinesInMessage = 5;

        private static readonly IReadOnlyDictionary<string, string> NoEnvironment = new Dictionary<string, string>();

        private readonly PluginsSettings _settings;
        private readonly UvLocator _uvLocator;
        private readonly IProcessRunner _runner;
        private readonly string _dependenciesRoot;
        private readonly bool _portable;
        private readonly string _uvCachePath;
        private readonly Action<string> _logError;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _pluginLocks = new();
        private readonly ConcurrentDictionary<string, string> _pythonVersions = new(StringComparer.OrdinalIgnoreCase);

        public PythonDependencyInstaller(
            PluginsSettings settings,
            UvLocator uvLocator,
            IProcessRunner runner,
            string dependenciesRoot,
            bool portable,
            string uvCachePath,
            Action<string>? logError = null)
        {
            _settings = settings;
            _uvLocator = uvLocator;
            _runner = runner;
            _dependenciesRoot = dependenciesRoot;
            _portable = portable;
            _uvCachePath = uvCachePath;
            _logError = logError ?? (_ => { });
        }

        public static PythonDependencyInstaller CreateDefault(PluginsSettings settings)
        {
            return new PythonDependencyInstaller(
                settings,
                UvLocator.CreateDefault(settings),
                new ProcessRunner(),
                DataLocation.PythonDependenciesPath,
                DataLocation.PortableDataLocationInUse(),
                DataLocation.UvCachePath,
                message => PublicApi.Instance.LogError(nameof(PythonDependencyInstaller), message));
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

            var pluginLock = _pluginLocks.GetOrAdd(metadata.ID, _ => new SemaphoreSlim(1, 1));
            await pluginLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await EnsureInstalledLockedAsync(metadata, dependencies, pythonExecutable, token).ConfigureAwait(false);
            }
            finally
            {
                pluginLock.Release();
            }
        }

        public void CleanupStaleInstalls(string pluginId)
        {
            if (!IsUsableAsFolderName(pluginId))
            {
                _logError($"Skipped cleaning up Python dependencies of plugin ID '{pluginId}' because it cannot be used as a folder name");
                return;
            }

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

        public void RemovePlugin(string pluginId)
        {
            if (!IsUsableAsFolderName(pluginId))
            {
                _logError($"Skipped removing Python dependencies of plugin ID '{pluginId}' because it cannot be used as a folder name");
                return;
            }

            var pluginRoot = Path.Combine(_dependenciesRoot, pluginId);
            try
            {
                if (Directory.Exists(pluginRoot))
                {
                    Directory.Delete(pluginRoot, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logError($"Failed to remove Python dependencies of plugin {pluginId}: {e.Message}");
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

        private async Task<string> GetPythonVersionAsync(PluginMetadata metadata, string python, CancellationToken token)
        {
            if (_pythonVersions.TryGetValue(python, out var cached))
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

            return _pythonVersions[python] = result.StdOut.Trim();
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
            try
            {
                return await _runner.RunAsync(fileName, arguments, useUvEnvironment ? BuildUvEnvironment() : NoEnvironment, token)
                    .ConfigureAwait(false);
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

        private IReadOnlyDictionary<string, string> BuildUvEnvironment()
        {
            var environment = new Dictionary<string, string> { ["UV_NO_CONFIG"] = "1" };
            if (_portable)
            {
                environment["UV_CACHE_DIR"] = _uvCachePath;
            }

            return environment;
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
