using System;
using System.IO;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public static class DataLocation
    {
        public const string PortableFolderName = "UserData";
        public const string DeletionIndicatorFile = ".dead";
        public static readonly string PortableDataPath = Path.Combine(Constant.ProgramDirectory, PortableFolderName);
        public static readonly string RoamingDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher");
        public static string DataDirectory()
        {
            if (PortableDataLocationInUse())
                return PortableDataPath;

            return RoamingDataPath;
        }

        public static bool PortableDataLocationInUse()
        {
            if (Directory.Exists(PortableDataPath) &&
                !File.Exists(Path.Combine(PortableDataPath, DeletionIndicatorFile)))
                return true;

            return false;
        }

        public static string VersionLogDirectory => Path.Combine(LogDirectory, Constant.Version);
        public static string LogDirectory => Path.Combine(DataDirectory(), Constant.Logs);

        public static readonly string CacheDirectory = Path.Combine(DataDirectory(), Constant.Cache);
        public static readonly string SettingsDirectory = Path.Combine(DataDirectory(), Constant.Settings);
        public static readonly string PluginsDirectory = Path.Combine(DataDirectory(), Constant.Plugins);
        public static readonly string ThemesDirectory = Path.Combine(DataDirectory(), Constant.Themes);

        public static readonly string PluginSettingsDirectory = Path.Combine(SettingsDirectory, Constant.Plugins);
        public static readonly string PluginCacheDirectory = Path.Combine(DataDirectory(), Constant.Cache, Constant.Plugins);

        public const string PythonEnvironmentName = "Python";
        public const string NodeEnvironmentName = "Node.js";
        public const string PluginEnvironments = "Environments";
        public const string PluginDeleteFile = "NeedDelete.txt";
        public static readonly string PluginEnvironmentsPath = Path.Combine(DataDirectory(), PluginEnvironments);

        /// <summary>
        /// Resolves a path that may be relative to an absolute path.
        /// If the path is already absolute, returns it as-is.
        /// If the path is not rooted (as determined by <see cref="Path.IsPathRooted(string)"/>), resolves it relative to ProgramDirectory.
        /// </summary>
        /// <param name="path">The path to resolve</param>
        /// <returns>An absolute path</returns>
        public static string ResolveAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // If already absolute, return as-is
            if (Path.IsPathRooted(path))
                return path;

            // Resolve relative to ProgramDirectory, handling invalid path formats gracefully
            try
            {
                return Path.GetFullPath(Path.Combine(Constant.ProgramDirectory, path));
            }
            catch (System.Exception ex) when (ex is ArgumentException ||
                                       ex is NotSupportedException ||
                                       ex is PathTooLongException)
            {
                // If the path cannot be resolved (invalid characters, format, or too long),
                // return the original path to avoid crashing the application.
                return path;
            }
        }
    }
}
