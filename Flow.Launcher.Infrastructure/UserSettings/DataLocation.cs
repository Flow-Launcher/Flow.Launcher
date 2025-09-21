using System;
using System.IO;

#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public static class DataLocation
    {
        public const string PortableFolderName = "UserData";
        public const string DeletionIndicatorFile = ".dead";
        public static string PortableDataPath = Path.Combine(Constant.ProgramDirectory, PortableFolderName);
        public static string RoamingDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher");
        public static string DataDirectory()
        {
            if (PortableDataLocationInUse())
                return PortableDataPath;

            return RoamingDataPath;
        }

        public static bool PortableDataLocationInUse()
        {
            if (Directory.Exists(PortableDataPath) && !File.Exists(DeletionIndicatorFile))
                return true;

            return false;
        }

        public static string VersionLogDirectory => Path.Combine(LogDirectory, Constant.Version);
        public static string LogDirectory => Path.Combine(DataDirectory(), Constant.Logs);

        public static string CacheDirectory => Path.Combine(DataDirectory(), Constant.Cache);
        public static string SettingsDirectory => Path.Combine(DataDirectory(), Constant.Settings);
        public static string PluginsDirectory => Path.Combine(DataDirectory(), Constant.Plugins);
        public static string ThemesDirectory => Path.Combine(DataDirectory(), Constant.Themes);

        public static string PluginSettingsDirectory => Path.Combine(SettingsDirectory, Constant.Plugins);
        public static string PluginCacheDirectory => Path.Combine(DataDirectory(), Constant.Cache, Constant.Plugins);

        public const string PythonEnvironmentName = "Python";
        public const string NodeEnvironmentName = "Node.js";
        public const string PluginEnvironments = "Environments";
        public static string PluginEnvironmentsPath => Path.Combine(DataDirectory(), PluginEnvironments);
    }
}
