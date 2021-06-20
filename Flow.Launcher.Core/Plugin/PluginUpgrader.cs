using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.Plugin
{
    public static class PluginUpgrader
    {
        /// <summary>
        /// Indicates these plugins need to have version greater than currently recorded
        /// </summary>
        private static readonly Dictionary<string, PluginVersion> PluginsUpgradeRequiredVersion = new()
        {
            { "D2D2C23B084D411DB66FE0C79D6C2A6E", new PluginVersion { ForceUpgradeRequiredVersion = "1.4.9" }  }
        };

        internal static void ForceUpgrade(PluginsSettings settings)
        {
            // if new version is added for PluginVersion.NewVersionDownloadUrl=> remove older plugin, download & extract
            // if PluginVersion.NewVersionDownloadUrl does not contain new version url, disable plugin and prompt to notify user
        }

        internal static bool ForceUpgradeRequired(PluginsSettings settings)
        {
            return 
                settings.Plugins
                .Any(t1 => PluginsUpgradeRequiredVersion
                            .Any(x => x.Key == t1.Key && x.Value.ForceUpgradeRequiredVersion == t1.Value.Version));
        }
    }

    internal class PluginVersion
    {
        internal string ForceUpgradeRequiredVersion;

        internal string NewVersionDownloadUrl = string.Empty;
    }
}
