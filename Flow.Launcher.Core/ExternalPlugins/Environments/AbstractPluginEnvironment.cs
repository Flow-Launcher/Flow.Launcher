using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Flow.Launcher.Core.Resource;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    public abstract class AbstractPluginEnvironment
    {
        protected readonly IPublicAPI API = Ioc.Default.GetRequiredService<IPublicAPI>();

        internal abstract string Language { get; }

        internal abstract string EnvName { get; }

        internal abstract string EnvPath { get; }

        internal abstract string InstallPath { get; }

        internal abstract string ExecutablePath { get; }

        internal virtual string FileDialogFilter => string.Empty;

        internal abstract string PluginsSettingsFilePath { get; set; }

        internal List<PluginMetadata> PluginMetadataList;

        internal PluginsSettings PluginSettings;

        internal AbstractPluginEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings)
        {
            PluginMetadataList = pluginMetadataList;
            PluginSettings = pluginSettings;
        }

        internal IEnumerable<PluginPair> Setup()
        {
            if (!PluginMetadataList.Any(o => o.Language.Equals(Language, StringComparison.OrdinalIgnoreCase)))
                return new List<PluginPair>();

            if (!string.IsNullOrEmpty(PluginsSettingsFilePath) && FilesFolders.FileExists(PluginsSettingsFilePath))
            {
                // Ensure latest only if user is using Flow's environment setup.
                if (PluginsSettingsFilePath.StartsWith(EnvPath, StringComparison.OrdinalIgnoreCase))
                    EnsureLatestInstalled(ExecutablePath, PluginsSettingsFilePath, EnvPath);

                return SetPathForPluginPairs(PluginsSettingsFilePath, Language);
            }

            var noRuntimeMessage = string.Format(
                InternationalizationManager.Instance.GetTranslation("runtimePluginInstalledChooseRuntimePrompt"),
                Language,
                EnvName,
                Environment.NewLine
            );
            if (API.ShowMsgBox(noRuntimeMessage, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                var msg = string.Format(InternationalizationManager.Instance.GetTranslation("runtimePluginChooseRuntimeExecutable"), EnvName);
                string selectedFile;

                selectedFile = GetFileFromDialog(msg, FileDialogFilter);

                if (!string.IsNullOrEmpty(selectedFile))
                    PluginsSettingsFilePath = selectedFile;

                // Nothing selected because user pressed cancel from the file dialog window
                if (string.IsNullOrEmpty(selectedFile))
                    InstallEnvironment();
            }
            else
            {
                InstallEnvironment();
            }

            if (FilesFolders.FileExists(PluginsSettingsFilePath))
            {
                return SetPathForPluginPairs(PluginsSettingsFilePath, Language);
            }
            else
            {
                API.ShowMsgBox(string.Format(InternationalizationManager.Instance.GetTranslation("runtimePluginUnableToSetExecutablePath"), Language));
                Log.Error("PluginsLoader",
                    $"Not able to successfully set {EnvName} path, setting's plugin executable path variable is still an empty string.",
                    $"{Language}Environment");

                return new List<PluginPair>();
            }
        }

        internal abstract void InstallEnvironment();

        private void EnsureLatestInstalled(string expectedPath, string currentPath, string installedDirPath)
        {
            if (expectedPath == currentPath)
                return;

            FilesFolders.RemoveFolderIfExists(installedDirPath, (s) => API.ShowMsgBox(s));

            InstallEnvironment();

        }

        internal abstract PluginPair CreatePluginPair(string filePath, PluginMetadata metadata);

        private IEnumerable<PluginPair> SetPathForPluginPairs(string filePath, string languageToSet)
        {
            var pluginPairs = new List<PluginPair>();

            foreach (var metadata in PluginMetadataList)
            {
                if (metadata.Language.Equals(languageToSet, StringComparison.OrdinalIgnoreCase))
                    pluginPairs.Add(CreatePluginPair(filePath, metadata));
            }

            return pluginPairs;
        }

        private string GetFileFromDialog(string title, string filter = "")
        {
            var dlg = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Title = title,
                Filter = filter
            };

            var result = dlg.ShowDialog();
            return result == DialogResult.OK ? dlg.FileName : string.Empty;

        }

        /// <summary>
        /// After app updated while in portable mode or switched between portable/roaming mode,
        /// need to update each plugin's executable path so user will not be prompted again to reinstall the environments.
        /// </summary>
        /// <param name="settings"></param>
        public static void PreStartPluginExecutablePathUpdate(Settings settings)
        {
            if (DataLocation.PortableDataLocationInUse())
            {
                // When user is using portable but has moved flow to a different location
                if (IsUsingPortablePath(settings.PluginSettings.PythonExecutablePath, DataLocation.PythonEnvironmentName)
                    && !settings.PluginSettings.PythonExecutablePath.StartsWith(DataLocation.PortableDataPath))
                {
                    settings.PluginSettings.PythonExecutablePath
                        = GetUpdatedEnvironmentPath(settings.PluginSettings.PythonExecutablePath);
                }

                if (IsUsingPortablePath(settings.PluginSettings.NodeExecutablePath, DataLocation.NodeEnvironmentName)
                    && !settings.PluginSettings.NodeExecutablePath.StartsWith(DataLocation.PortableDataPath))
                {
                    settings.PluginSettings.NodeExecutablePath
                        = GetUpdatedEnvironmentPath(settings.PluginSettings.NodeExecutablePath);
                }

                // When user has switched from roaming to portable
                if (IsUsingRoamingPath(settings.PluginSettings.PythonExecutablePath))
                {
                    settings.PluginSettings.PythonExecutablePath
                        = settings.PluginSettings.PythonExecutablePath.Replace(DataLocation.RoamingDataPath, DataLocation.PortableDataPath);
                }

                if (IsUsingRoamingPath(settings.PluginSettings.NodeExecutablePath))
                {
                    settings.PluginSettings.NodeExecutablePath
                        = settings.PluginSettings.NodeExecutablePath.Replace(DataLocation.RoamingDataPath, DataLocation.PortableDataPath);
                }
            }
            else
            {
                if (IsUsingPortablePath(settings.PluginSettings.PythonExecutablePath, DataLocation.PythonEnvironmentName))
                    settings.PluginSettings.PythonExecutablePath
                        = GetUpdatedEnvironmentPath(settings.PluginSettings.PythonExecutablePath);

                if (IsUsingPortablePath(settings.PluginSettings.NodeExecutablePath, DataLocation.NodeEnvironmentName))
                    settings.PluginSettings.NodeExecutablePath
                        = GetUpdatedEnvironmentPath(settings.PluginSettings.NodeExecutablePath);
            }
        }

        private static bool IsUsingPortablePath(string filePath, string pluginEnvironmentName)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // DataLocation.PortableDataPath returns the current portable path, this determines if an out
            // of date path is also a portable path.
            var portableAppEnvLocation = $"UserData\\{DataLocation.PluginEnvironments}\\{pluginEnvironmentName}";

            return filePath.Contains(portableAppEnvLocation);
        }

        private static bool IsUsingRoamingPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            return filePath.StartsWith(DataLocation.RoamingDataPath);
        }

        private static string GetUpdatedEnvironmentPath(string filePath)
        {
            var index = filePath.IndexOf(DataLocation.PluginEnvironments);
            
            // get the substring after "Environments" because we can not determine it dynamically
            var ExecutablePathSubstring = filePath.Substring(index + DataLocation.PluginEnvironments.Count());
            return $"{DataLocation.PluginEnvironmentsPath}{ExecutablePathSubstring}";
        }
    }
}
