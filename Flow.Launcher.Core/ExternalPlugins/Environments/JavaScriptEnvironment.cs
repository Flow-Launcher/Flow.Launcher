using System.Collections.Generic;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{

    internal class JavaScriptEnvironment : TypeScriptEnvironment
    {
        internal override string Language => AllowedLanguage.JavaScript;

        internal JavaScriptEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }
    }
}
