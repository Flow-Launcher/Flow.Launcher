using System;
using System.IO;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public static class DataLocation
    {
        static DataLocation()
        {
            // check whether the package locate in %LocalAppData%
            // if not create the portable data folder
            // Don't create the folder if the version is 1.0.0 (Dev) to allow potential debugging with data in the project folder
            // It is still possible to create the UserData folder for dev version manually but we want to keep the current behavior
            if (!Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    .PathContains(Constant.ProgramDirectory)
                && Constant.Version != "1.0.0")
            {
                Directory.CreateDirectory(PortableDataPath);
            }

            PluginsDirectory = Path.Combine(DataDirectory(), Constant.Plugins);
            PluginEnvironmentsPath = Path.Combine(DataDirectory(), PluginEnvironments);
        }

        public const string PortableFolderName = "UserData";
        public const string DeletionIndicatorFile = ".dead";
        public static string PortableDataPath = Path.Combine(Constant.ProgramDirectory, PortableFolderName);

        public static string RoamingDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher")

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

        public static readonly string PluginsDirectory;

        public static readonly string PluginSettingsDirectory =
            Path.Combine(DataDirectory(), "Settings", Constant.Plugins);

        public const string PythonEnvironmentName = "Python";
        public const string NodeEnvironmentName = "Node.js";
        public const string PluginEnvironments = "Environments";
        public static readonly string PluginEnvironmentsPath;
    }
}
