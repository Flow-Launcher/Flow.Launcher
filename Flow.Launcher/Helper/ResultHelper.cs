using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;

namespace Flow.Launcher.Helper;

#nullable enable

public static class ResultHelper
{
    public static async Task<Result?> PopulateResultsAsync(LastOpenedHistoryItem item)
    {
        return await PopulateResultsAsync(item.PluginID, item.Query, item.Title, item.SubTitle, item.RecordKey);
    }

    public static async Task<Result?> PopulateResultsAsync(string pluginId, string rawQuery, string title, string subTitle, string recordKey)
    {
        var plugin = PluginManager.GetPluginForId(pluginId);
        if (plugin == null) return null;
        var query = QueryBuilder.Build(rawQuery, PluginManager.NonGlobalPlugins);
        if (query == null) return null;
        try
        {
            var freshResults = await plugin.Plugin.QueryAsync(query, CancellationToken.None);
            // Try to match by record key first if it is valid, otherwise fall back to title + subtitle match
            if (string.IsNullOrEmpty(recordKey))
            {
                return freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle);
            }
            else
            {
                return freshResults?.FirstOrDefault(r => r.RecordKey == recordKey) ??
                    freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle);
            }
        }
        catch
        {
            return null;
        }
    }

    public static bool IsEquals(this Result result, LastOpenedHistoryItem item, HistoryStyle style)
    {
        bool keyMatches = string.IsNullOrEmpty(result.RecordKey) && string.IsNullOrEmpty(item.RecordKey)
                        ? item.Title == result.Title
                           : !string.IsNullOrEmpty(result.RecordKey) && !string.IsNullOrEmpty(item.RecordKey) && item.RecordKey == result.RecordKey;

        bool queryMatches = style != HistoryStyle.Query || (result.OriginQuery != null && item.Query == result.OriginQuery.RawQuery);


        return keyMatches
               && queryMatches
               && item.PluginID == result.PluginID
               && item.HistoryStyle == style;
    }
}
