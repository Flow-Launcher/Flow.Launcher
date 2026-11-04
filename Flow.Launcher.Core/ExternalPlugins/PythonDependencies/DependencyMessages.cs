using System;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    /// <summary>
    /// Localized messages for dependency failures. Falls back to plain text when the translation
    /// service is not available (for example in unit tests).
    /// </summary>
    internal static class DependencyMessages
    {
        public static string NotPinned(string pluginName, string unpinned)
        {
            return Translate(
                () => Localize.pythonDependencyNotPinned(pluginName, unpinned),
                $"Plugin {pluginName} lists unpinned Python dependencies: {unpinned}");
        }

        public static string UvUnavailable(string pluginName)
        {
            return Translate(
                () => Localize.pythonDependencyUvUnavailable(pluginName),
                $"Could not find or download uv for plugin {pluginName}");
        }

        public static string InstallFailed(string pluginName, string lastLines)
        {
            var details = string.IsNullOrEmpty(lastLines) ? string.Empty : Environment.NewLine + lastLines;
            return Translate(
                () => Localize.pythonDependencyInstallFailed(pluginName, details),
                $"Failed to install Python dependencies for plugin {pluginName}{details}");
        }

        public static string InvalidPluginId(string pluginName)
        {
            return Translate(
                () => Localize.pythonDependencyInvalidPluginId(pluginName),
                $"Plugin {pluginName} has an ID that cannot be used as a folder name");
        }

        private static string Translate(Func<string> localized, string fallback)
        {
            try
            {
                return localized();
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
