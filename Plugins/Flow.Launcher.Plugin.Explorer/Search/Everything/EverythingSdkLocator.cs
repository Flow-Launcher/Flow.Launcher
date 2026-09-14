using System.IO;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static class EverythingSdkLocator
    {
        public static string GetSdkDirectory(string pluginDirectory, Architecture processArchitecture)
        {
            return processArchitecture == Architecture.X64
                ? Path.Combine(pluginDirectory, "EverythingSDK", "x64")
                : null;
        }
    }
}
