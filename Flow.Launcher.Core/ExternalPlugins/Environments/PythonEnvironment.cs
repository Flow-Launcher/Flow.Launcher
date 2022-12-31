using Droplex;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal class PythonEnvironment : AbstractPluginEnvironment
    {
        internal override string Language => AllowedLanguage.Python;

        internal override string EnvName => DataLocation.PythonEnvironmentName;

        internal override string EnvPath => Path.Combine(DataLocation.PluginEnvironmentsPath, EnvName);

        internal override string InstallPath => Path.Combine(EnvPath, "PythonEmbeddable-v3.8.9");

        internal override string ExecutablePath => Path.Combine(InstallPath, "pythonw.exe");

        internal override string FileDialogFilter => "Python|pythonw.exe";

        internal override string PluginsSettingsFilePath { get => PluginSettings.PythonExecutablePath; set => PluginSettings.PythonExecutablePath = value; }

        internal PythonEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath);

            // Python 3.8.9 is used for Windows 7 compatibility
            DroplexPackage.Drop(App.python_3_8_9_embeddable, InstallPath).Wait();

            PluginsSettingsFilePath = ExecutablePath;
        }

        internal override PluginPair CreatePluginPair(string filePath, PluginMetadata metadata)
        {
            return new PluginPair
            {
                Plugin = new PythonPlugin(filePath),
                Metadata = metadata
            };
        }
    }
}
