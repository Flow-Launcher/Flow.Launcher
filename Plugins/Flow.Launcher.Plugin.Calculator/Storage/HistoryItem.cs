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
        Title = expression;
        SubTitle = CreateSubTitle(result.Title);
        Score = 300;
        IcoPath = result.IcoPath;
        Query = expression;
        BadgeIcoPath = BadgeIconPath;
        ShowBadge = true;
        CopyText = result.Title;
        Action = action;
    }

    public void Refresh(Result result, Func<ActionContext, bool> action)
    {
        CalculatedAt = DateTime.Now;
        SubTitle = CreateSubTitle(result.Title);
        CopyText = result.Title;
        Action = action;
    }


    private string CreateSubTitle(string value)
    {
        return
            $"{value} - {Localize.flowlauncher_plugin_calculator_copy_number_to_clipboard()}" +
            $"\n{Localize.flowlauncher_plugin_calculator_history_subtitle(CalculatedAt)}";
    }
}
