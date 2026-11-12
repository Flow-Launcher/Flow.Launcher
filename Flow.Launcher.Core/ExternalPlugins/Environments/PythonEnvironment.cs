using System.Collections.Generic;
using System.IO;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class PythonEnvironment : AbstractPluginEnvironment
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
