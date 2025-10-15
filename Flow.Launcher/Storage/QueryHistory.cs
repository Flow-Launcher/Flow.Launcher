using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
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

        private readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

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

            var style = _settings.HistoryStyle;
            // Maintain the max history limit
           if (LastOpenedHistoryItems.Count > _maxHistory)
            {
                LastOpenedHistoryItems.RemoveAt(0);
            }

            // If the last item is the same as the current result, just update the timestamp
            if (LastOpenedHistoryItems.Count > 0)
            {
                var last = LastOpenedHistoryItems.Last();
                if (result.IsEquals(last, style))
                {
                    last.ExecutedDateTime = DateTime.Now;
                    return;
                }

                var existItem = LastOpenedHistoryItems.FirstOrDefault(x => result.IsEquals(x, style));

                if (existItem != null)
                {
                    existItem.ExecutedDateTime = DateTime.Now;
                    return;
                }
            }

            LastOpenedHistoryItems.Add(new LastOpenedHistoryItem
            {
                Title = result.Title,
                SubTitle = result.SubTitle,
                PluginID = result.PluginID,
                Query = result.OriginQuery.RawQuery,
                RecordKey = result.RecordKey,
                ExecutedDateTime = DateTime.Now,
                HistoryStyle = style
            });
        }

       
    }
}
