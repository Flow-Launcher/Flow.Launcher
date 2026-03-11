using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class Pinned
    {
        private readonly int _maxPinned = 10;
        public List<PinnedResultItem> Items { set; get; } = [];


        public void Add(Result result, string query = "")
        {
            if (string.IsNullOrEmpty(result.PluginID)) return;
            if (Items.Count >= _maxPinned)
            {
                Items.RemoveAt(0);
            }

            if (Items.Count > 0 &&
            TryGetPinnedResult(result, query,out var existingPinnedResult))
            {
                existingPinnedResult.AddAt = DateTime.Now; 

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

        public void AddOrRemove(Result result, string query, bool? exist = null)
        {
            if (exist == null)
            {
                exist = Exists(result, query);
            } 

            if (!exist.Value)
            {
                Add(result, query);
                return;
            }
            Remove(result);
        }

        public void Remove(Result result)
        {
            var itemToRemove = Items.FirstOrDefault(x => x.Equals(result));
            if (itemToRemove != null) Items.Remove(itemToRemove);
        }

        public bool Exists(Result result, string? query = null)
        {
            if (string.IsNullOrEmpty(query)) return Items.Any(x => x.Equals(result));
            return Items.Any(x => x.Equals(result, query));
        }

        private bool TryGetPinnedResult(Result result, string query, out PinnedResultItem item)
        {
            if (!string.IsNullOrEmpty(query)) item =  Items.FirstOrDefault(x => x.Equals(result, query));
            else item = Items.FirstOrDefault(x => x.Equals(result));
            return item is not null;
        }

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
