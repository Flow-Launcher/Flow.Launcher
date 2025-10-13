using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure.UserSettings;
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

        public void AddToHistory(Result result, Settings settings)
        {
            if (!settings.ShowHistoryOnHomePage) return;
            if (settings.ShowHistoryQueryResultsForHomePage)
            { 
                AddLastQuery(result);
                return;
            }
            AddLastOpened(result);
        }


        public List<HistoryItem> GetHistoryItems(Settings  settings)
        {
            if (settings.ShowHistoryQueryResultsForHomePage) return QueryHistoryItems.PopulateActions(true);
            return LastOpenedHistoryItems.PopulateActions(false);
        }

        public void PopulateHistoryFromLegacyHistory()
        {
            foreach (var item in Items)
            {
                QueryHistoryItems.Add(new HistoryItem
                {
                    RawQuery = item.Query,
                    ExecutedDateTime = item.ExecutedDateTime ?? DateTime.Now,
                    QueryAction = HistoryHelper.GetQueryAction(item.Query)
                });
            }
            if (Items.Count > 0) Items.Clear();
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
                    QueryAction = HistoryHelper.GetQueryAction(result.OriginQuery.RawQuery)
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
                RecordKey = result.RecordKey,
                ExecutedDateTime = DateTime.Now,
                ExecuteAction =  result.Action
            };

            var existing = LastOpenedHistoryItems.
                FirstOrDefault(x => x.Title == item.Title &&  x.PluginID == item.PluginID);


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
