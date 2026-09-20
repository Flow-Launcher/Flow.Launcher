using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Calculator.Storage;

public class HistoryItem : Result
{
    public string Query { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }

    [JsonIgnore] private const string BadgeIconPath = "Images/history.png";

    public HistoryItem()
    {
    }

    public HistoryItem(Result item ,string result, string expression, DateTime calculatedAt)
    {
        CalculatedAt = calculatedAt;
        Title = expression;
        SubTitle = result;
        Score = 300;
        IcoPath = item.IcoPath;
        Query = expression;
        BadgeIcoPath = BadgeIconPath;
        ShowBadge = true;
        CopyText = item.Title;
        Action = item.Action;

    }


    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryItem"/> class from a <see cref="PendingHistoryItem"/>.
    /// </summary>
    /// <param name="item">The pending history item to copy properties from.</param>
    public HistoryItem(PendingHistoryItem item)
        : this(item.Result, item.SubTitle, item.Expression, item.CalculatedAt)
    {
    }
}
