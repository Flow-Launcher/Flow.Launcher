using System.Collections.Generic;
using System.IO;
using Droplex;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal class TypeScriptV2Environment : AbstractPluginEnvironment
    {
        private static readonly string ClassName = nameof(TypeScriptV2Environment);

        internal override string Language => AllowedLanguage.TypeScriptV2;

        internal override string EnvName => DataLocation.NodeEnvironmentName;

        internal override string EnvPath => Path.Combine(DataLocation.PluginEnvironmentsPath, EnvName);

        internal override string InstallPath => Path.Combine(EnvPath, "Node-v16.18.0");
        internal override string ExecutablePath => Path.Combine(InstallPath, "node-v16.18.0-win-x64\\node.exe");

        internal override string PluginsSettingsFilePath
        {
            get => PluginSettings.NodeExecutablePath;
            set => PluginSettings.NodeExecutablePath = value;
        }

        internal TypeScriptV2Environment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }

        private JoinableTaskFactory JTF { get; } = new JoinableTaskFactory(new JoinableTaskContext());

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, (s) => API.ShowMsgBox(s));

            JTF.Run(async () =>
            {
                try
                {
                    await DroplexPackage.Drop(App.nodejs_16_18_0, InstallPath);

                    PluginsSettingsFilePath = ExecutablePath;
                }
                catch (System.Exception e)
                {
                    API.ShowMsgError(Localize.failToInstallTypeScriptEnv());
                    API.LogException(ClassName, "Failed to install TypeScript environment", e);
                }
            });
        }

        internal override PluginPair CreatePluginPair(string filePath, PluginMetadata metadata)
        {
            return new PluginPair
            {
                Plugin = new NodePluginV2(filePath),
                Metadata = metadata
            };
        }
    }
}
