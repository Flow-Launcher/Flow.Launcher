using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;

namespace Flow.Launcher.Plugin.Calculator.Storage;

public class History
{
    private const int MaxItems = 5;
    private const int DebounceDelayMs = 800;

    [JsonInclude]
    public List<HistoryItem> Items { get; set; } = [];

    [JsonIgnore]
    private readonly Lock _syncRoot = new();

    [JsonIgnore]
    private Timer _debounceTimer;

    [JsonIgnore]
    private PendingHistoryItem _pendingItem;

    public void AddOrUpdate(PendingHistoryItem pendingItem)
    {
        lock (_syncRoot)
        {
            _pendingItem = pendingItem;

            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer(_ => FlushPendingItem(), null, DebounceDelayMs, Timeout.Infinite);
            }
            else
            {
                _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
            }
        }
    }

    public List<HistoryItem> GetItemsExcluding(string expression)
    {
        lock (_syncRoot)
        {
            return Items
                .Where(x => x.Query != expression)
                .OrderByDescending(x => x.CalculatedAt)
                .ToList();
        }
    }

    private void FlushPendingItem()
    {
        lock (_syncRoot)
        {
            if (_pendingItem == null)
            {
                return;
            }

            var currentItem = Items.FirstOrDefault(x => x.Query == _pendingItem.Expression);
            if (currentItem != null)
            {
                currentItem.Refresh(_pendingItem);
            }
            else
            {
                var item = new HistoryItem(_pendingItem);

                if (Items.Count >= MaxItems)
                {
                    Items.RemoveAt(0);
                }

                Items.Add(item);
            }

            _pendingItem = null;
        }
    }
}
