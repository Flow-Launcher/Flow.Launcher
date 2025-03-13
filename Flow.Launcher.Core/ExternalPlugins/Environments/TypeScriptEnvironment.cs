using System.Collections.Generic;
using Droplex;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin;
using System.IO;
using Flow.Launcher.Core.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal class TypeScriptEnvironment : AbstractPluginEnvironment
    {
        internal override string Language => AllowedLanguage.TypeScript;

        internal override string EnvName => DataLocation.NodeEnvironmentName;

        internal override string EnvPath => Path.Combine(DataLocation.PluginEnvironmentsPath, EnvName);

        internal override string InstallPath => Path.Combine(EnvPath, "Node-v16.18.0");
        internal override string ExecutablePath => Path.Combine(InstallPath, "node-v16.18.0-win-x64\\node.exe");

        internal override string PluginsSettingsFilePath { get => PluginSettings.NodeExecutablePath; set => PluginSettings.NodeExecutablePath = value; }

        internal TypeScriptEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, (s) => API.ShowMsgBox(s));

            DroplexPackage.Drop(App.nodejs_16_18_0, InstallPath).Wait();

            PluginsSettingsFilePath = ExecutablePath;
        }

        internal override PluginPair CreatePluginPair(string filePath, PluginMetadata metadata)
        {
            return new PluginPair
            {
                Plugin = new NodePlugin(filePath),
                Metadata = metadata
            };
        }
    }
}
