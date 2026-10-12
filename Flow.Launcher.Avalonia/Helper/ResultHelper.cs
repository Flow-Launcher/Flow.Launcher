using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.Helper;

internal static class ResultHelper
{
    internal static async Task<Result?> PopulateResultsAsync(string pluginId, string trimmedQuery, string title, string subTitle, string recordKey)
    {
        var plugin = PluginManager.GetPluginForId(pluginId);
        if (plugin == null)
        {
            return null;
        }

        var query = QueryBuilder.Build(trimmedQuery, trimmedQuery, PluginManager.GetNonGlobalPlugins());
        if (query == null)
        {
            return null;
        }

        try
        {
            var freshResults = await PluginManager.QueryForPluginAsync(plugin, query, CancellationToken.None);
            if (string.IsNullOrEmpty(recordKey))
            {
                return freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle);
            }

            return freshResults?.FirstOrDefault(r => r.RecordKey == recordKey)
                ?? freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle);
        }
        catch (System.Exception e)
        {
            App.API?.LogException(nameof(ResultHelper), $"Failed to query results for plugin id {pluginId}", e);
            return null;
        }
    }
}
