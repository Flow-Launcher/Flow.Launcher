using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class History
    {
        [JsonInclude]
#pragma warning disable CS0618 // Type or member is obsolete
        public List<HistoryItem> Items { get; private set; } = [];
#pragma warning restore CS0618 // Type or member is obsolete

        [JsonInclude]
        public List<LastOpenedHistoryItem> LastOpenedHistoryItems { get; private set; } = [];

        private readonly int _maxHistory = 300;

        public void PopulateHistoryFromLegacyHistory()
        {
            if (Items.Count == 0) return;
            // Migrate old history items to new LastOpenedHistoryItems
            foreach (var item in Items)
            {
                LastOpenedHistoryItems.Add(new LastOpenedHistoryItem
                {
                    Query = item.Query,
                    ExecutedDateTime = item.ExecutedDateTime
                });
            }
            Items.Clear();
        }

        public void Add(Result result)
        {
            if (string.IsNullOrEmpty(result.OriginQuery.RawQuery)) return;
            if (string.IsNullOrEmpty(result.PluginID)) return;

            // Maintain the max history limit
            if (LastOpenedHistoryItems.Count > _maxHistory)
            {
                LastOpenedHistoryItems.RemoveAt(0);
            }

            if (LastOpenedHistoryItems.Count > 0 &&
                TryGetLastOpenedHistoryResult(result, out var existingHistoryItem))
            {
                existingHistoryItem.IcoPath = result.IcoPath;
                existingHistoryItem.ExecutedDateTime = DateTime.Now;
            }
            else
            {
                LastOpenedHistoryItems.Add(new LastOpenedHistoryItem
                {
                    Title = result.Title,
                    SubTitle = result.SubTitle,
                    PluginID = result.PluginID,
                    Query = result.OriginQuery.RawQuery,
                    RecordKey = result.RecordKey,
                    IcoPath = result.IcoPath,
                    Glyph = result.Glyph,
                    ExecutedDateTime = DateTime.Now
                });
            }
        }

        private bool TryGetLastOpenedHistoryResult(Result result, out LastOpenedHistoryItem historyItem)
        {
            historyItem = LastOpenedHistoryItems.FirstOrDefault(x => x.Equals(result));
            return historyItem is not null;
        }
    }
}
