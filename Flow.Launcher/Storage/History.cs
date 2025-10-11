using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{

    public class History
    {
        //Legacy
        [JsonInclude] public List<HistoryItemLegacy> Items { get; private set; } = [];
        [JsonInclude] public List<HistoryItem> LastOpenedHistoryItems { get; private set; } = [];
        [JsonInclude] public List<HistoryItem> QueryHistoryItems { get; private set; } = [];

        private int _maxHistory = 300;

        public void AddToHistory(Result result, bool isQuery)
        {
            if (isQuery)
            {
                AddLastQuery(result); 
                return;
            }

            AddLastOpened(result);
        }


        public List<HistoryItem> GetHistoryItems(bool isQuery)
        {
            if (isQuery) return PopulateActions(QueryHistoryItems, isQuery);
            return PopulateActions(LastOpenedHistoryItems, isQuery);
        }

        public void PopulateHistoryWithLegacyHistory()
        {
            foreach (var item in Items)
            {
                QueryHistoryItems.Add(new HistoryItem
                {
                    RawQuery = item.Query,
                    ExecutedDateTime = item.ExecutedDateTime ?? DateTime.Now,
                    QueryAction = GetQueryAction(item.Query)
                });
            }
            if (Items.Any()) Items.Clear();
        }

        private List<HistoryItem> PopulateActions(List<HistoryItem> items,bool isQuery)
        {

            foreach (var item in items)
            {
                if (item.QueryAction != null && item.ExecuteAction != null) continue;
                if (isQuery && item.QueryAction == null) item.QueryAction = GetQueryAction(item.RawQuery);
                if (!isQuery && item.ExecuteAction == null) item.ExecuteAction = GetExecuteAction(item.PluginID, item.RawQuery, item.Title, item.SubTitle);
            }

            return items;
        }

        private Func<ActionContext, bool> GetExecuteAction(string pluginId, string rawQuery, string title, string subTitle)
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
        private Func<ActionContext, bool> GetQueryAction(string query)
        {
            return _=>
            {
                App.API.BackToQueryResults();
                App.API.ChangeQuery(query);
                return false;
            };
        }

        private void AddLastQuery(Result result)
        {
            if (string.IsNullOrEmpty(result.OriginQuery.RawQuery)) return;
            if (QueryHistoryItems.Count > _maxHistory)
            {
                QueryHistoryItems.RemoveAt(0);
            }

            if (QueryHistoryItems.Count > 0 && QueryHistoryItems.Last().RawQuery == result.OriginQuery.RawQuery)
            {
                QueryHistoryItems.Last().ExecutedDateTime = DateTime.Now;
            }
            else
            {
                QueryHistoryItems.Add(new HistoryItem
                {
                    RawQuery = result.OriginQuery.RawQuery,
                    ExecutedDateTime = DateTime.Now,
                    QueryAction = GetQueryAction(result.OriginQuery.RawQuery)
                });
            }
        }

        private void AddLastOpened(Result result)
        {
            var item = new HistoryItem
            {
                Title = result.Title,
                SubTitle = result.SubTitle,
                PluginID = result.PluginID,
                RawQuery = result.OriginQuery.RawQuery,
                ExecutedDateTime = DateTime.Now,
                ExecuteAction =  result.Action
            };

            var existing = LastOpenedHistoryItems.
                FirstOrDefault(x => x.Title == item.Title && x.PluginID == item.PluginID);


            if (existing != null)
            {
                existing.ExecutedDateTime = DateTime.Now;
            }
            else
            {
                if (LastOpenedHistoryItems.Count > _maxHistory)
                {
                    LastOpenedHistoryItems.RemoveAt(0);
                }

                LastOpenedHistoryItems.Add(item);
            }
        }
    }
}
