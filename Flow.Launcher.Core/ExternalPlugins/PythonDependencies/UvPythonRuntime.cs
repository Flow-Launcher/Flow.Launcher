#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class UvPythonRuntime
    {
        public const string Version = "3.11.17";

        private const string X64InstallKey = "cpython-" + Version + "-windows-x86_64-none";
        private const string X86InstallKey = "cpython-" + Version + "-windows-x86-none";

        // The full key keeps the install folder name deterministic.
        public static string InstallKey { get; } = InstallKeyFor(RuntimeInformation.OSArchitecture);

        // ARM64 Windows also gets the x64 build and runs it under emulation: plugins bundle x64 wheels in lib,
        // and win_arm64 wheels are still missing for many packages.
        public static string InstallKeyFor(Architecture architecture)
        {
            return architecture == Architecture.X86 ? X86InstallKey : X64InstallKey;
        }

        public static bool IsInstalledIn(string pythonExecutable, string runtimeDirectory)
        {
            try
            {
                var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(runtimeDirectory)) + Path.DirectorySeparatorChar;
                return Path.GetFullPath(pythonExecutable).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        // Kept apart from the plugin dependency folders so uv's own metadata never mixes with them.
        public const string InstallDirectoryName = "Runtime";
    }
}
