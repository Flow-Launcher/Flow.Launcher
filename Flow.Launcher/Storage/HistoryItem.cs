using System;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

public class HistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string PluginID { get; set; } = string.Empty;
    public string RawQuery { get; set; }
    public string RecordKey { get; set; } = string.Empty;
    public DateTime ExecutedDateTime { get; set; }

    [JsonIgnore]
    public Func<ActionContext, bool> ExecuteAction { get; set; }

    [JsonIgnore]
    public Func<ActionContext, bool> QueryAction { get; set; }
}
