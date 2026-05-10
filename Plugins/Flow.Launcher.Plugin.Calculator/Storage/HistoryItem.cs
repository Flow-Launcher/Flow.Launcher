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

    public HistoryItem(PendingHistoryItem item)
    {
        CalculatedAt = item.CalculatedAt;
        Title = item.Expression;
        SubTitle = item.SubTitle;
        Score = 300;
        IcoPath = item.Result.IcoPath;
        Query = item.Expression;
        BadgeIcoPath = BadgeIconPath;
        ShowBadge = true;
        CopyText = item.Result.Title;
        Action = item.Action;
    }

    public void Refresh(PendingHistoryItem item)
    {
        CalculatedAt = item.CalculatedAt;
        SubTitle = item.SubTitle;
        CopyText = item.Result.Title;
        Action = item.Action;
    }

}
