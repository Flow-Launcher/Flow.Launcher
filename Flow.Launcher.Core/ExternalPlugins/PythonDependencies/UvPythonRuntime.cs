#nullable enable

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class UvPythonRuntime
    {
        public const string Version = "3.11.17";

        // The full key keeps the install folder name deterministic. uv has no Windows ARM64 build of 3.11,
        // so ARM64 Windows runs this x64 build under emulation.
        public const string InstallKey = "cpython-3.11.17-windows-x86_64-none";

        // Kept apart from the plugin dependency folders so uv's own metadata never mixes with them.
        public const string InstallDirectoryName = "Runtime";
    }
}
