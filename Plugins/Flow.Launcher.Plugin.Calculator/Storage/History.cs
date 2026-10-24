using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;

namespace Flow.Launcher.Plugin.Calculator.Storage;

/// <summary>
/// Manages the collection of calculation history items, providing methods to add, update, and retrieve items.
/// <para>
/// Supports two creation modes:
/// <list type="bullet">
/// <item>
/// <description><b>On Query Mode:</b> Uses <see cref="PendingHistoryItem"/> with a debounce timer to save calculations automatically as the user types without logging incomplete states.</description>
/// </item>
/// <item>
/// <description><b>On Enter Mode:</b> Adds or updates a standard <see cref="HistoryItem"/> immediately without any debounce when the calculation is executed.</description>
/// </item>
/// </list>
/// </para>
/// </summary>
public class History
{
    private const int MaxItems = 5;
    private const int DebounceDelayMs = 800;

    /// <summary>
    /// Gets or sets the list of saved history items.
    /// </summary>
    [JsonInclude]
    public List<HistoryItem> Items { get; set; } = [];

    [JsonIgnore]
    private readonly Lock _syncRoot = new();

    [JsonIgnore]
    private Timer _debounceTimer;

    [JsonIgnore]
    private PendingHistoryItem _pendingItem;

    /// <summary>
    /// Adds a completed history item immediately without any debounce, or updates its timestamp if the query already exists.
    /// Used when HistoryCreationMode is set to OnEnter.
    /// </summary>
    /// <param name="item">The history item to add or update.</param>
    public void AddOrUpdate(HistoryItem item)
    {
        lock (_syncRoot)
        {
            AddOrUpdateInternal(item);
        }
    }

    /// <summary>
    /// Unlocked internal helper to add a history item, or update its fields if the query already exists.
    /// </summary>
    /// <param name="item">The history item to add or update.</param>
    private void AddOrUpdateInternal(HistoryItem item)
    {
        var currentItem = Items.FirstOrDefault(x => x.Query == item.Query);
        if (currentItem != null)
        {
            currentItem.CalculatedAt = item.CalculatedAt;
            currentItem.CalcResult = item.CalcResult;
        }
        else
        {
            Add(item);
        }
    }

    /// <summary>
    /// Schedules a pending history item to be added or updated after a debounce delay.
    /// Used when HistoryCreationMode is set to OnQuery to prevent spamming the history with incomplete queries as the user types.
    /// </summary>
    /// <param name="pendingItem">The pending history item to be debounced.</param>
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

    /// <summary>
    /// Retrieves a list of history items, sorted by calculation date in descending order, excluding the specified query.
    /// </summary>
    /// <param name="query">The query to exclude from the results.</param>
    /// <returns>A list of history items matching the criteria.</returns>
    public List<HistoryItem> GetItemsExcluding(string query)
    {
        lock (_syncRoot)
        {
            return Items
                .Where(x => x.Query != query)
                .OrderByDescending(x => x.CalculatedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Adds a history item to the list, removing the oldest calculation if the maximum count is exceeded.
    /// </summary>
    /// <param name="item">The history item to add.</param>
    private void Add(HistoryItem item)
    {
        if (Items.Count >= MaxItems)
        {
            Items.Remove(Items.MinBy(x => x.CalculatedAt)!);
        }
        Items.Add(item);
    }

    /// <summary>
    /// Cancels a pending debounced history item so it is not saved, for example when the
    /// query is cleared or becomes incomplete, or when history is disabled or switched to OnEnter.
    /// </summary>
    public void CancelPending()
    {
        lock (_syncRoot)
        {
            _pendingItem = null;
            // Stop the timer so a previously scheduled flush cannot commit the cancelled item.
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Immediately flushes the currently pending history item, bypassing the debounce delay.
    /// </summary>
    public void FlushPending()
        => FlushPendingItem();

    /// <summary>
    /// Flushes the currently pending history item by adding it to the list.
    /// </summary>
    private void FlushPendingItem()
    {
        lock (_syncRoot)
        {
            if (_pendingItem == null)
            {
                return;
            }

            var item = new HistoryItem(_pendingItem);
            AddOrUpdateInternal(item);

            _pendingItem = null;
        }
    }
}
