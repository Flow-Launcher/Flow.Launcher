#nullable enable

using System.Collections.Specialized;
using System.IO;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class PythonEnvironmentVariables
    {
        /// <summary>
        /// The embeddable Python Flow used to ship never loaded the user's site-packages, so plugins that bundle
        /// their libraries in lib could not be shadowed by packages the user installed with pip --user.
        /// Flow's uv runtime keeps it that way. A Python the user picked keeps its normal behaviour.
        /// </summary>
        public static void DisableUserSiteForFlowRuntime(StringDictionary environment, string pythonExecutable)
        {
            var runtimeDirectory = Path.Combine(
                DataLocation.PluginEnvironmentsPath, DataLocation.PythonEnvironmentName, UvPythonRuntime.InstallDirectoryName);
            if (UvPythonRuntime.IsInstalledIn(FilesFolders.ResolveAbsolutePath(pythonExecutable), runtimeDirectory))
            {
                environment["PYTHONNOUSERSITE"] = "1";
            }
        }
    }
}
