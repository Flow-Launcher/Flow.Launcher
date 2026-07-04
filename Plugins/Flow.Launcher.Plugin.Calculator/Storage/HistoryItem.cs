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
}
