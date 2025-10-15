using System;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

public class LastOpenedHistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string PluginID { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public HistoryStyle HistoryStyle { get; set; }
    public DateTime ExecutedDateTime { get; set; }

}
