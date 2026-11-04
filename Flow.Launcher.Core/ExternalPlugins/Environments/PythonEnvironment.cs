using System.Collections.Generic;
using System.IO;
using System.Threading;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
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

        internal override string InstallPath => Path.Combine(EnvPath, UvPythonRuntime.InstallDirectoryName, UvPythonRuntime.InstallKey);

        internal override string ExecutablePath => Path.Combine(InstallPath, "pythonw.exe");

        internal override string FileDialogFilter => "Python|pythonw.exe";

        internal override string PluginsSettingsFilePath
        {
            get => PluginSettings.PythonExecutablePath;
            set => PluginSettings.PythonExecutablePath = value;
        }

        protected readonly PythonDependencyInstaller _pythonDependencies;

        internal PythonEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings, PythonDependencyInstaller pythonDependencies) : base(pluginMetadataList, pluginSettings)
        {
            _pythonDependencies = pythonDependencies;
        }

        private JoinableTaskFactory JTF { get; } = new JoinableTaskFactory(new JoinableTaskContext());

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, (s) => API.ShowMsgBox(s));

            JTF.Run(async () =>
            {
                try
                {
                    PluginsSettingsFilePath = await CreateRuntimeInstaller().InstallAsync(CancellationToken.None);
                }
                catch (System.Exception e)
                {
                    API.ShowMsgError(Localize.failToInstallPythonEnv());
                    API.LogException(ClassName, "Failed to install Python environment", e);
                }
            });
        }

        private PythonRuntimeInstaller CreateRuntimeInstaller()
        {
            return new PythonRuntimeInstaller(
                _pythonDependencies.UvLocator,
                new ProcessRunner(),
                Path.Combine(EnvPath, UvPythonRuntime.InstallDirectoryName),
                DataLocation.PortableDataLocationInUse(),
                DataLocation.UvCachePath);
        }

        internal override PluginPair CreatePluginPair(string filePath, PluginMetadata metadata)
        {
            return new PluginPair
            {
                Plugin = new PythonPlugin(filePath, _pythonDependencies),
                Metadata = metadata
            };
        }
    }
}
