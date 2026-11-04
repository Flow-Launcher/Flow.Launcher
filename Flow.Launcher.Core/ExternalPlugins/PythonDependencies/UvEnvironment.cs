#nullable enable

using System.Collections.Generic;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class UvEnvironment
    {
        public static IReadOnlyDictionary<string, string> Build(bool portable, string uvCachePath)
        {
            var environment = new Dictionary<string, string> { ["UV_NO_CONFIG"] = "1" };
            if (portable)
            {
                environment["UV_CACHE_DIR"] = uvCachePath;
            }

            return environment;
        }
    }
}
