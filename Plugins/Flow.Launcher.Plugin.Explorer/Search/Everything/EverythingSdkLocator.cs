using System.IO;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static class EverythingSdkLocator
    {
        public static string GetSdkDirectory(string pluginDirectory, Architecture processArchitecture)
        {
            var architectureDirectory = processArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => null,
            };

            return architectureDirectory is null
                ? null
                : Path.Combine(pluginDirectory, "EverythingSDK", architectureDirectory);
        }
    }
}
