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
    internal class PythonEnvironment : AbstractPluginEnvironment
    {
        private static readonly string ClassName = nameof(PythonEnvironment);

        internal override string Language => AllowedLanguage.Python;

        internal override string EnvName => DataLocation.PythonEnvironmentName;

        internal override string EnvPath => Path.Combine(DataLocation.PluginEnvironmentsPath, EnvName);

        internal override string InstallPath => Path.Combine(EnvPath, "PythonEmbeddable-v3.11.4");

        internal override string ExecutablePath => Path.Combine(InstallPath, "pythonw.exe");

        internal override string FileDialogFilter => "Python|pythonw.exe";

        internal override string PluginsSettingsFilePath
        {
            get => PluginSettings.PythonExecutablePath;
            set => PluginSettings.PythonExecutablePath = value;
        }

        internal PythonEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }

        private JoinableTaskFactory JTF { get; } = new JoinableTaskFactory(new JoinableTaskContext());

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, (s) => API.ShowMsgBox(s));

            // Python 3.11.4 is no longer Windows 7 compatible. If user is on Win 7 and
            // uses Python plugin they need to custom install and use v3.8.9
            JTF.Run(async () =>
            {
                try
                {
                    await DroplexPackage.Drop(App.python_3_11_4_embeddable, InstallPath);

                    PluginsSettingsFilePath = ExecutablePath;
                }
                catch (System.Exception e)
                {
                    API.ShowMsgError(API.GetTranslation("failToInstallPythonEnv"));
                    API.LogException(ClassName, "Failed to install Python environment", e);
                }
            });
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
