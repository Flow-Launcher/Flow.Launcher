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
        /// If the path is relative (starts with . or doesn't contain a drive), resolves it relative to ProgramDirectory.
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

            // Resolve relative to ProgramDirectory
            return Path.GetFullPath(Path.Combine(Constant.ProgramDirectory, path));
        }

        /// <summary>
        /// Converts an absolute path to a relative path if it's within ProgramDirectory.
        /// This enables portability by storing paths relative to the program directory when possible.
        /// </summary>
        /// <param name="absolutePath">The absolute path to convert</param>
        /// <returns>A relative path if the path is within ProgramDirectory, otherwise the original absolute path</returns>
        public static string ConvertToRelativePathIfPossible(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return absolutePath;

            if (!Path.IsPathRooted(absolutePath))
                return absolutePath;

            try
            {
                // Get the full absolute paths for comparison
                var fullAbsolutePath = Path.GetFullPath(absolutePath);
                var fullProgramDir = Path.GetFullPath(Constant.ProgramDirectory);

                // Check if the absolute path is within ProgramDirectory
                if (fullAbsolutePath.StartsWith(fullProgramDir, StringComparison.OrdinalIgnoreCase))
                {
                    // Convert to relative path
                    var relativePath = Path.GetRelativePath(fullProgramDir, fullAbsolutePath);

                    // Prefix with .\ for clarity
                    if (!relativePath.StartsWith('.'))
                        relativePath = ".\\" + relativePath;

                    return relativePath;
                }
            }
            catch
            {
                // If conversion fails, return the original path
            }

            return absolutePath;
        }
    }
}
