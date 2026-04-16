using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class Pinned
    {
        private readonly int _maxPinned = 11;

        [JsonInclude]
        public List<PinnedResultItem> Items { set; get; } = [];

        /// <summary>
        /// Adds a result to the pinned items collection, updating the existing entry if it matches the last pinned
        /// item.
        /// </summary>
        /// <remarks>If the pinned items collection has reached its maximum limit, the oldest item is
        /// removed. If the last pinned item matches the provided result and query, its timestamp and icon information
        /// are updated instead of adding a new entry.</remarks>
        /// <param name="result">The result to be pinned. Must have a non-empty PluginID.</param>
        /// <param name="query">The query string associated with the result. Optional; defaults to an empty string.</param>
        public void Add(Result result, string query = "")
        {
            if (string.IsNullOrEmpty(result.PluginID)) return;

            // Maintain the max history limit
            if (Items.Count > _maxPinned)
            {
                Items.RemoveAt(0);
            }

            // If the last item is the same as the current result, just update the timestamp and the icon path
            if (Items.Count > 0 && TryGetPinnedResult(result, query, out var existingPinnedResult))
            {
                existingPinnedResult.LastPinnedAt = DateTime.Now; 

                if (existingPinnedResult.IcoPath != result.IcoPath)
                    existingPinnedResult.IcoPath = result.IcoPath;

                if (existingPinnedResult.Glyph?.Glyph != result.Glyph?.Glyph
                    || existingPinnedResult.Glyph?.FontFamily != result.Glyph?.FontFamily)
                    existingPinnedResult.SetGlyph(result.Glyph);
            }
            else 
            {
                Items.Add(new PinnedResultItem(result, query));
            }
        }

        /// <summary>
        /// Adds or removes the specified result based on its existence in the collection for the given query.
        /// </summary>
        /// <remarks>If <paramref name="exist"/> is not provided, the method checks for existence using
        /// the query. The result is added if it does not exist; otherwise, it is removed.</remarks>
        /// <param name="result">The result object to add or remove from the collection.</param>
        /// <param name="query">The query string used to determine the existence of the result in the collection.</param>
        /// <param name="exist">Indicates whether the result already exists in the collection. If null, existence is determined
        /// automatically.</param>
        public void AddOrRemove(Result result, string query, bool? exist = null)
        {
            exist ??= Exists(result, query);
            if (!exist.Value)
            {
                Add(result, query);
            }
            else
            {
                Remove(result, query);
            }
        }

        /// <summary>
        /// Removes the specified result from the collection, if it exists.
        /// </summary>
        /// <remarks>If the specified result is not found in the collection, no action is taken.</remarks>
        /// <param name="result">The result to remove from the collection. Must not be null.</param>
        private void Remove(Result result, string query)
        {
            if (TryGetPinnedResult(result, query, out var existingPinnedResult))
            {
                Items.Remove(existingPinnedResult);
            }
        }

        /// <summary>
        /// Determines whether the specified result exists in the collection, optionally using a query string for
        /// comparison.
        /// </summary>
        /// <param name="result">The result to search for within the collection.</param>
        /// <param name="query">An optional query string used to customize the comparison. If null or empty, a default comparison is
        /// performed.</param>
        /// <returns>true if the result exists in the collection; otherwise, false.</returns>
        public bool Exists(Result result, string query = null)
        {
            if (string.IsNullOrEmpty(query)) return Items.Any(x => x.Equals(result));
            return Items.Any(x => x.Equals(result, query));
        }

        /// <summary>
        /// Attempts to retrieve a pinned result item that matches the specified result and query.
        /// </summary>
        /// <remarks>Use this method to efficiently check for and retrieve a pinned result item based on a
        /// result and optional query. The method does not throw exceptions if no match is found.</remarks>
        /// <param name="result">The result to match against pinned items.</param>
        /// <param name="query">The query string used to refine the search. If null or empty, the search is performed without query
        /// filtering.</param>
        /// <param name="item">When this method returns, contains the pinned result item if a match is found; otherwise, null.</param>
        /// <returns>true if a matching pinned result item is found; otherwise, false.</returns>
        private bool TryGetPinnedResult(Result result, string query, out PinnedResultItem item)
        {
            if (!string.IsNullOrEmpty(query))
            {
                item = Items.FirstOrDefault(x => x.Equals(result, query));
            }
            else
            {
                item = Items.FirstOrDefault(x => x.Equals(result));
            }
            return item is not null;
        }

        /// <summary>
        /// Updates the absolute plugin directory path for each item in the collection based on its associated plugin
        /// identifier.
        /// </summary>
        /// <remarks>Only items with a non-empty plugin identifier are updated. If a plugin cannot be
        /// found for an item's identifier, that item is skipped.</remarks>
        public void UpdateIcoPathAbsolute()
        {
            if (Items.Count == 0) return;

            foreach (var item in Items)
            {
                if (string.IsNullOrEmpty(item.PluginID)) continue;

                var pluginPair = PluginManager.GetPluginForId(item.PluginID);
                if (pluginPair == null) continue;

                item.PluginDirectory = pluginPair.Metadata.PluginDirectory;
            }
        }
    }
}
