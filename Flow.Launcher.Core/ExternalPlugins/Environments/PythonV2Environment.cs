using System.Collections.Generic;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal class PythonV2Environment : PythonEnvironment
    {
        internal override string Language => AllowedLanguage.PythonV2;

        internal PythonV2Environment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }
    }
}
