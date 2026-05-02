using System;

namespace Flow.Launcher.Plugin.Calculator.Storage;

internal class HistoryItem : Result
{
    public string Query { get; set; }
    public DateTime CalculatedAt { get; set; }

    public HistoryItem(Result result, Func<ActionContext, bool> action)
    {
        Title = result.Title;
        SubTitle = result.SubTitle;
        //PluginID = result.PluginID;
        //Query = result.OriginQuery.TrimmedQuery;
        //OriginQuery = result.OriginQuery;
        RecordKey = result.RecordKey;
        IcoPath = result.IcoPath;
        PluginDirectory = result.PluginDirectory;
        Glyph = result.Glyph;
        //Query = result.Ori
        Query = "1+2";
        Action = action;
    }
}
