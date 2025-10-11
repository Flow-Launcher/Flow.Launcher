using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;
public static class HistoryHelper
{
    internal static  List<HistoryItem> PopulateActions(this List<HistoryItem> items, bool isQuery)
    {

        foreach (var item in items)
        {
            if (item.QueryAction != null && item.ExecuteAction != null) continue;
            if (isQuery && item.QueryAction == null) item.QueryAction = GetQueryAction(item.RawQuery);
            if (!isQuery && item.ExecuteAction == null) item.ExecuteAction = GetExecuteAction(item.PluginID, item.RawQuery, item.Title, item.SubTitle);
        }

        return items;
    }

    private static Func<ActionContext, bool> GetExecuteAction(string pluginId, string rawQuery, string title, string subTitle)
    {
        var plugin = PluginManager.GetPluginForId(pluginId);

        var query = QueryBuilder.Build(rawQuery, PluginManager.NonGlobalPlugins);
        var freshResults = plugin.Plugin
            .QueryAsync(query, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return freshResults?.FirstOrDefault(r => r.Title == title
                                                 && r.SubTitle == subTitle)?.Action;
    }
    public static Func<ActionContext, bool> GetQueryAction(string query)
    {
        return _ =>
        {
            App.API.BackToQueryResults();
            App.API.ChangeQuery(query);
            return false;
        };
    }
}
