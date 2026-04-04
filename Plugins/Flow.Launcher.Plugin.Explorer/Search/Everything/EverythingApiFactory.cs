using System;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    internal static class EverythingApiFactory
    {
        internal const string DefaultEverything15InstanceName = "1.5a";

        public static IEverythingApi Create(Settings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return settings.EnableEverything15Support
                ? new EverythingApiV3(GetNormalizedInstanceName(settings.Everything15InstanceName))
                : new LegacyEverythingApi();
        }

        internal static string GetNormalizedInstanceName(string instanceName) => string.IsNullOrWhiteSpace(instanceName)
            ? DefaultEverything15InstanceName
            : instanceName.Trim();
    }
}
