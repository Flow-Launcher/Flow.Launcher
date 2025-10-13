using System;

namespace Flow.Launcher.Storage;

[Obsolete]
public class HistoryItemLegacy
{
    public string Query { get; set; }
    public DateTime? ExecutedDateTime { get; set; }
}
