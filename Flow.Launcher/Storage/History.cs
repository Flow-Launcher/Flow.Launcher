using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using JetBrains.Annotations;

namespace Flow.Launcher.Storage
{
    public class History
    {
        [JsonInclude] 
        public List<HistoryItem> LastOpenedHistoryItems { get; private set; } = [];
        [JsonInclude] 
        public List<HistoryItem> QueryHistoryItems { get; private set; } = [];

        private int _maxHistory = 300;

        public void AddToHistory(Result result, Settings settings)
        { 
            if (settings.ShowHistoryQueryResultsForHomePage)
            {
                AddLastQuery(result);
                return;
            }
            AddLastOpened(result);
        }

        public List<HistoryItem> GetHistoryItems(Settings settings)
        { 
            if (settings.ShowHistoryQueryResultsForHomePage) return QueryHistoryItems;
            if (settings.ShowHistoryLastOpenedResultsForHomePage) return LastOpenedHistoryItems;
            return new List<HistoryItem>();
        }

        private void AddLastQuery(Result result)
        {
            if (string.IsNullOrEmpty(result.OriginQuery.RawQuery)) return;
            if (QueryHistoryItems.Count > _maxHistory)
            {
                QueryHistoryItems.RemoveAt(0);
            }

            if (QueryHistoryItems.Count > 0 && QueryHistoryItems.Last().OriginQuery.RawQuery == result.OriginQuery.RawQuery)
            {
                QueryHistoryItems.Last().ExecutedDateTime = DateTime.Now;
            }
            else
            {
                QueryHistoryItems.Add(new HistoryItem
                {
                    OriginQuery = result.OriginQuery,
                    ExecutedDateTime = DateTime.Now,
                    QueryAction = _ =>
                    {
                        App.API.BackToQueryResults();
                        App.API.ChangeQuery(result.OriginQuery.RawQuery);
                        return false;
                    }
                });
            }
        }

        private void AddLastOpened(Result result)
        {
            var item = new HistoryItem
            {
                Title = result.Title,
                SubTitle = result.SubTitle,
                IcoPath = result.IcoPath ?? string.Empty,
                PluginID = result.PluginID,
                OriginQuery = result.OriginQuery,
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
