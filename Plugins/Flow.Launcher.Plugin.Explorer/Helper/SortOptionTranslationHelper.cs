using Flow.Launcher.Plugin.Everything.Everything;
using JetBrains.Annotations;
using System;

namespace Flow.Launcher.Plugin.Explorer.Helper;

public static class SortOptionTranslationHelper
{
    [CanBeNull]
    public static IPublicAPI API { get; internal set; }

    public static string GetTranslatedName(this SortOption sortOption)
    {
        const string prefix = "flowlauncher_plugin_everything_sort_by_";

        ArgumentNullException.ThrowIfNull(API);

        var enumName = Enum.GetName(sortOption);
        var splited = enumName.Split('_');
        var name = string.Join('_', splited[..^1]);
        var direction = splited[^1];

        return $"{API.GetTranslation(prefix + name.ToLower())} {API.GetTranslation(prefix + direction.ToLower())}";
    }
}
