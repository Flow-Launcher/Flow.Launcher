using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Calculator.Storage;

public class HistoryItem : Result
{
    public string Query { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }

    [JsonIgnore]
    private const string BadgeIconPath = "Images/history.png";
    public HistoryItem()
    {
    }

    public HistoryItem(Result result, string expression, Func<ActionContext, bool> action)
    {
        CalculatedAt = DateTime.Now;
        Title = $"{expression} = {result.Title}";
        SubTitle = Localize.flowlauncher_plugin_calculator_history_subtitle(CalculatedAt);
        Score = 300;
        IcoPath = result.IcoPath;
        Query = expression;
        BadgeIcoPath = BadgeIconPath;
        ShowBadge = true;
        CopyText = result.Title;
        Action = action;
    }

}
