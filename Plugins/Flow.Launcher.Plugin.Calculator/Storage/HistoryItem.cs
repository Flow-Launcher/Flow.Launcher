using System;

namespace Flow.Launcher.Plugin.Calculator.Storage;

/// <summary>
/// Represents a single persisted calculation history entry.
/// <para>
/// Only the data required to rebuild a result is stored. Presentation details
/// (title, subtitle, icon and action) are rebuilt when the item is materialized
/// for a query so that non-serializable members such as the click action are not lost.
/// </para>
/// </summary>
public class HistoryItem
{
    internal const string IcoPath = "Images/calculator.png";
    internal const string BadgeIconPath = "Images/history.png";
    internal const int HistoryItemScore = 300;

    public string Query { get; set; } = string.Empty;

    public string CalcResult { get; set; } = string.Empty;

    public DateTime CalculatedAt { get; set; }

    public HistoryItem()
    {
    }

    public HistoryItem(string query, string calcResult, DateTime calculatedAt)
    {
        Query = query;
        CalcResult = calcResult;
        CalculatedAt = calculatedAt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryItem"/> class from a <see cref="PendingHistoryItem"/>.
    /// </summary>
    /// <param name="item">The pending history item to copy properties from.</param>
    public HistoryItem(PendingHistoryItem item)
        : this(item.Query, item.CalcResult, item.CalculatedAt)
    {
    }
}
