using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;
public class HistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string IcoPath { get; set; } = string.Empty;
    public string PluginID { get; set; } = string.Empty;
    public Query OriginQuery { get; set; } = null!;
    public DateTime ExecutedDateTime { get; set; }
    public Func<ActionContext, bool> ExecuteAction { get; set; }
    public Func<ActionContext, bool> QueryAction { get; set; }

}
