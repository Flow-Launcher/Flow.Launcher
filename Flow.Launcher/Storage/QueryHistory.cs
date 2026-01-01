using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
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
        public List<LastOpenedHistoryResult> LastOpenedHistoryItems { get; private set; } = [];

        private readonly int _maxHistory = 300;

        public void PopulateHistoryFromLegacyHistory()
        {
            if (Items.Count == 0) return;
            // Migrate old history items to new LastOpenedHistoryItems
            foreach (var item in Items)
            {
                LastOpenedHistoryItems.Add(new LastOpenedHistoryResult
                {
                    Title = Localize.executeQuery(item.Query),
                    IcoPath = Constant.HistoryIcon,
                    OriginQuery = new Query { RawQuery = item.Query },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE81C"),
                    Query = item.Query,
                    Action = _ =>
                    {
                        App.API.BackToQueryResults();
                        App.API.ChangeQuery(item.Query);
                        return false;
                    },
                    ExecutedDateTime = item.ExecutedDateTime
                });
            }
            Items.Clear();
        }

        public void Add(Result result)
        {
            if (string.IsNullOrEmpty(result.OriginQuery.TrimmedQuery)) return;
            if (string.IsNullOrEmpty(result.PluginID)) return;

            // Maintain the max history limit
            if (LastOpenedHistoryItems.Count > _maxHistory)
            {
                LastOpenedHistoryItems.RemoveAt(0);
            }

            if (LastOpenedHistoryItems.Count > 0 &&
                TryGetLastOpenedHistoryResult(result, out var existingHistoryItem))
            {
                //existingHistoryItem.IcoPath = result.IcoPath;
                existingHistoryItem.ExecutedDateTime = DateTime.Now;
            }
            else
            {
                LastOpenedHistoryItems.Add(new LastOpenedHistoryResult(result));
            }
        }

        private bool TryGetLastOpenedHistoryResult(Result result, out LastOpenedHistoryResult historyItem)
        {
            historyItem = LastOpenedHistoryItems.FirstOrDefault(x => x.Equals(result));
            return historyItem is not null;
        }

        /// <summary>
        /// Refresh stored PluginDirectory (and optionally normalize relative ico paths)
        /// using current plugin metadata. Call this after plugins are loaded/initialized.
        /// </summary>
        public void UpdateIcoAbsoluteFullPath()
        {
            if (LastOpenedHistoryItems.Count == 0) return;

            foreach (var item in LastOpenedHistoryItems)
            {
                if (string.IsNullOrEmpty(item.PluginID))
                    continue;

                var pluginPair = PluginManager.GetPluginForId(item.PluginID);
                if (pluginPair == null)
                    continue;

                //item.IcoPath = Path.Combine(pluginPair.Metadata.PluginDirectory, item.IcoPath);

                item.PluginDirectory = pluginPair.Metadata.PluginDirectory;
            }
        }
    }
}
