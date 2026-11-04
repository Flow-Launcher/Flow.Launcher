#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal sealed class UvLocator
    {
        private const string ExecutableName = "uv.exe";

        private readonly PluginsSettings _settings;
        private readonly string _managedRoot;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string?> _findOnPath;
        private readonly Func<string, CancellationToken, Task<string>> _downloadAndExtract;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private string? _uvPath;

        public UvLocator(
            PluginsSettings settings,
            string managedRoot,
            Func<string, bool> fileExists,
            Func<string?> findOnPath,
            Func<string, CancellationToken, Task<string>> downloadAndExtract)
        {
            _settings = settings;
            _managedRoot = managedRoot;
            _fileExists = fileExists;
            _findOnPath = findOnPath;
            _downloadAndExtract = downloadAndExtract;
        }

        public static UvLocator CreateDefault(PluginsSettings settings)
        {
            return new UvLocator(
                settings,
                Path.Combine(DataLocation.PluginEnvironmentsPath, DataLocation.UvEnvironmentName),
                File.Exists,
                FindUvOnPath,
                UvDownloader.DownloadAndExtractAsync);
        }

        public async Task<string> GetUvPathAsync(CancellationToken token)
        {
            await _lock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return _uvPath ??= await LocateAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string> LocateAsync(CancellationToken token)
        {
            var configured = FilesFolders.ResolveAbsolutePath(_settings.UvExecutablePath);
            if (!string.IsNullOrEmpty(configured) && _fileExists(configured))
            {
                if (!IsStaleManagedInstall(configured, out var staleVersionDir))
                {
                    return configured;
                }

                TryDeleteDirectory(staleVersionDir);
            }

            var onPath = _findOnPath();
            if (onPath is not null)
            {
                return onPath;
            }

            var installDir = Path.Combine(_managedRoot, UvRelease.Version);
            var downloaded = await _downloadAndExtract(installDir, token).ConfigureAwait(false);
            _settings.UvExecutablePath = downloaded;
            return downloaded;
        }

        private bool IsStaleManagedInstall(string uvPath, out string staleVersionDir)
        {
            staleVersionDir = string.Empty;
            string root, current, path;
            try
            {
                root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_managedRoot));
                current = Path.Combine(root, UvRelease.Version);
                path = Path.GetFullPath(uvPath);
            }
            catch (Exception)
            {
                return false;
            }

            if (!IsInside(path, root) || IsInside(path, current))
            {
                return false;
            }

            var relative = Path.GetRelativePath(root, path);
            var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            staleVersionDir = Path.Combine(root, firstSegment);
            return true;
        }

        private static bool IsInside(string path, string directory)
        {
            var prefix = directory + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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
            catch (Exception)
            {
                // The new version is installed next to it, so a leftover folder is harmless.
            }
        }

        private static string? FindUvOnPath()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVariable))
            {
                return null;
            }

            foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim().Trim('"'), ExecutableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Skip PATH entries that are not valid paths.
                }
            }

            return null;
        }
    }
}
