#nullable enable

using System;
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

        private static readonly string[] KeptFolderNames = { UvPythonRuntime.InstallDirectoryName, DependenciesFolderName };

        /// <summary>
        /// Installs the new runtime first. Only when that works is the path updated and the old install removed,
        /// so a failed install leaves the setting and the old install untouched.
        /// </summary>
        public static async Task<bool> TryAsync(
            Func<Task<string>> install,
            Action<string> setPath,
            Action removeOldInstall,
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
            removeOldInstall();
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

            foreach (var entry in Directory.EnumerateFileSystemEntries(envPath).ToList())
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
