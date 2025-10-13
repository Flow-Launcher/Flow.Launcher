using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

#nullable enable

public static class HistoryHelper
{
    internal static List<HistoryItem> PopulateActions(this List<HistoryItem> items, bool isQuery)
    {

        foreach (var item in items)
        {
            if (item.QueryAction != null && item.ExecuteAction != null) continue;
            if (isQuery && item.QueryAction == null) item.QueryAction = GetQueryAction(item.RawQuery);
            if (!isQuery && item.ExecuteAction == null) item.ExecuteAction = GetExecuteAction(item.PluginID, item.RawQuery, item.Title, item.SubTitle, item.RecordKey) ?? GetQueryAction(item.RawQuery);
        }

        return items;
    }

    public static Func<ActionContext, bool> GetQueryAction(string rawQuery)
    {
        return _ =>
        {
            App.API.BackToQueryResults();
            App.API.ChangeQuery(rawQuery);
            return false;
        };
    }

    private static Func<ActionContext, bool>? GetExecuteAction(string pluginId, string rawQuery, string title, string subTitle, string recordKey)
    {
        var plugin = PluginManager.GetPluginForId(pluginId);
        if (plugin == null) return null;
        var query = QueryBuilder.Build(rawQuery, PluginManager.NonGlobalPlugins);
        if (query == null) return null;
        try
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            var freshResults = plugin.Plugin
                .QueryAsync(query, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            // Try to match by record key first if it is valid, otherwise fall back to title + subtitle match
            if (string.IsNullOrEmpty(recordKey))
            {
                return freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle)?.Action;
            }
            else
            {
                return freshResults?.FirstOrDefault(r => r.RecordKey == recordKey)?.Action ??
                    freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle)?.Action;
            }
        }
        catch
        {
            return null;
        }
    }
}
