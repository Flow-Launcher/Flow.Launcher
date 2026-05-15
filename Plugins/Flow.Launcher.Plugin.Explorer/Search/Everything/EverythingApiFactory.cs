using System;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    internal static class EverythingApiFactory
    {
        public static IEverythingApi Create(Settings settings)
        {
            return settings.EnableEverything15Support
                ? new EverythingApiV3(settings.Everything15InstanceName)
                : new LegacyEverythingApi();
        }
    }
}
