#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    /// <summary>
    /// Moves Flow from its previous Python install to the uv-managed one without ever leaving
    /// the user without a working Python.
    /// </summary>
    internal static class PythonRuntimeSwitch
    {
        private const string DependenciesFolderName = "PluginDependencies";

        private static readonly string[] KeptFolderNames =
        {
            UvPythonRuntime.InstallDirectoryName,
            DependenciesFolderName,
            DependenciesFolderName + PythonDependencyInstaller.PendingRemovalsSuffix
        };

        /// <summary>
        /// Installs the new runtime first and only updates the path when that works, so a failed install
        /// leaves the setting untouched. The old install is left for <see cref="RemoveStale"/> on a later start,
        /// once the new path has been saved.
        /// </summary>
        public static async Task<bool> TryAsync(
            Func<Task<string>> install,
            Action<string> setPath,
            Action<Exception> onFailure)
        {
            string path;
            try
            {
                path = await install().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onFailure(e);
                return false;
            }

            setPath(path);
            return true;
        }

        /// <summary>
        /// Deletes everything directly under the Python environment folder except the uv runtime and the plugin dependencies.
        /// </summary>
        public static void RemoveStale(string envPath, Action<string> logError, Action<string>? delete = null)
        {
            delete ??= DeleteFileOrDirectory;
            if (!Directory.Exists(envPath))
            {
                return;
            }

            List<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(envPath).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                logError($"Could not list {envPath} to remove old Python installs: {e.Message}");
                return;
            }

            foreach (var entry in entries)
            {
                if (KeptFolderNames.Contains(Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    delete(entry);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    logError($"Could not remove the old Python install {entry}: {e.Message}");
                }
            }
        }

        private static void DeleteFileOrDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }
        }
    }
}
