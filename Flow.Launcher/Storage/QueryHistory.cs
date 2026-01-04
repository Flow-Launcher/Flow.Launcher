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

        /// <summary>
        /// Migrate legacy history data (stored in <see cref="Items"/>) into the new
        /// <see cref="LastOpenedHistoryResult"/> format and append them to
        /// <see cref="LastOpenedHistoryItems"/>.
        /// </summary>
        [Obsolete("For backwards compatibility. Remove after release v2.3.0")]
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
                    OriginQuery = new Query { TrimmedQuery = item.Query },
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

        /// <summary>
        /// Records a result into the last-opened history list (<see cref="LastOpenedHistoryItems"/>).
        /// This will also update the IcoPath if existing history item has one that is different.
        /// </summary>
        /// <param name="result">The result to add to history. Must have a non-empty <see cref="Result.OriginQuery"/>.<see cref="Query.TrimmedQuery"/>.</param>
        public void Add(Result result)
        {
            if (string.IsNullOrEmpty(result.OriginQuery.TrimmedQuery)) return;
            // History results triggered from homepage do not contain PluginID,
            // these are intentionally not saved otherwise cause duplicates due to subtitle
            // containing datetime string.
            if (string.IsNullOrEmpty(result.PluginID)) return;

            // Maintain the max history limit
            if (LastOpenedHistoryItems.Count > _maxHistory)
            {
                LastOpenedHistoryItems.RemoveAt(0);
            }

            if (LastOpenedHistoryItems.Count > 0 &&
                TryGetLastOpenedHistoryResult(result, out var existingHistoryItem))
            {
                existingHistoryItem.ExecutedDateTime = DateTime.Now;

                if (existingHistoryItem.IcoPath != result.IcoPath)
                    existingHistoryItem.IcoPath = result.IcoPath;
            }
            else
            {
                LastOpenedHistoryItems.Add(new LastOpenedHistoryResult(result));
            }
        }

        /// <summary>
        /// Attempts to find an existing <see cref="LastOpenedHistoryResult"/> in <see cref="LastOpenedHistoryItems"/>
        /// that is considered equal to the supplied <paramref name="result"/>.
        /// </summary>
        private bool TryGetLastOpenedHistoryResult(Result result, out LastOpenedHistoryResult historyItem)
        {
            historyItem = LastOpenedHistoryItems.FirstOrDefault(x => x.Equals(result));
            return historyItem is not null;
        }

        /// <summary>
        /// Flow uses IcoPathAbsolute property to display result the icons. This refreshes the IcoPathAbsolute
        /// property using current plugin metadata by updating the PluginDirectory property, which in turn also
        /// updates IcoPath. This keeps the saved icon paths of results updated correctly if flow is moved around.
        /// </summary>
        /// <remarks> Call this after plugins are loaded/initialized.</remarks>
        public void UpdateIcoPathAbsolute()
        {
            if (LastOpenedHistoryItems.Count == 0)
                return;

            foreach (var item in LastOpenedHistoryItems)
            {
                if (string.IsNullOrEmpty(item.PluginID))
                    continue;

                var pluginPair = PluginManager.GetPluginForId(item.PluginID);
                if (pluginPair == null)
                    continue;

                item.PluginDirectory = pluginPair.Metadata.PluginDirectory;
            }
        }
    }
}
