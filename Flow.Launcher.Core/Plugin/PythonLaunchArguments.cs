#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    internal static class PythonLaunchArguments
    {
        public static IReadOnlyList<string> Build(PluginMetadata metadata, string? sitePackages, bool v1)
        {
            if (metadata.ExecuteFilePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "-c", BuildScriptSnippet(metadata, sitePackages) };
            }

            if (sitePackages is null)
            {
                // No need for -B flag because we're using PYTHONDONTWRITEBYTECODE env variable now,
                // but the plugins still expect data to be sent as the third argument, so V1 keeps
                // the flag here, even though it's not necessary anymore.
                return v1
                    ? new[] { "-B", metadata.ExecuteFilePath }
                    : new[] { metadata.ExecuteFilePath };
            }

            return new[] { "-c", BuildArchiveSnippet(metadata, sitePackages) };
        }

        // This makes it easier for plugin authors to import their own modules.
        // They won't have to add `.`, `./lib`, or `./plugin` to their sys.path manually.
        // Instead of running the .py file directly, we pass the code we want to run as a CLI argument.
        // This code sets sys.path for the plugin author and then runs the .py file via runpy.
        private static string BuildScriptSnippet(PluginMetadata metadata, string? sitePackages)
        {
            var rootDirectory = metadata.PluginDirectory;
            var libDirectory = Path.Combine(rootDirectory, "lib");
            var libPyWin32Directory = Path.Combine(libDirectory, "win32");
            var libPyWin32LibDirectory = Path.Combine(libPyWin32Directory, "lib");
            var pluginDirectory = Path.Combine(rootDirectory, "plugin");

            var lines = new List<string>
            {
                "import sys",
                $"sys.path.append(r'{rootDirectory}')"
            };

            // addsitedir appends, so adding it before lib gives the installed dependencies priority over lib.
            if (sitePackages is not null)
            {
                lines.AddRange(SiteDirectoryLines(sitePackages));
            }

            lines.Add($"sys.path.append(r'{libDirectory}')");
            lines.Add($"sys.path.append(r'{libPyWin32LibDirectory}')");
            lines.Add($"sys.path.append(r'{libPyWin32Directory}')");
            lines.Add($"sys.path.append(r'{pluginDirectory}')");
            lines.Add("");
            lines.AddRange(RunPathLines(metadata.ExecuteFilePath));

            return string.Join("\n", lines);
        }

        private static string BuildArchiveSnippet(PluginMetadata metadata, string sitePackages)
        {
            var lines = new List<string>(SiteDirectoryLines(sitePackages));
            lines.Add("");
            lines.AddRange(RunPathLines(metadata.ExecuteFilePath));
            return string.Join("\n", lines);
        }

        private static string[] SiteDirectoryLines(string sitePackages) => new[]
        {
            "import site",
            $"site.addsitedir(r'{sitePackages}')"
        };

        private static string[] RunPathLines(string executeFilePath) => new[]
        {
            "import runpy",
            $"runpy.run_path(r'{executeFilePath}', None, '__main__')"
        };
    }
}
