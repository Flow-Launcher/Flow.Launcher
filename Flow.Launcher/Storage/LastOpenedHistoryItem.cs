using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

public class LastOpenedHistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string PluginID { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public string IcoPath { get; set; } = string.Empty;
    public GlyphInfo Glyph { get; init; } = null;
    public DateTime ExecutedDateTime { get; set; }

    public bool Equals(Result r)
    {
        if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
        {
            return Title == r.Title
                && SubTitle == r.SubTitle
                && PluginID == r.PluginID
                && Query == r.OriginQuery.RawQuery;
        }
        else
        {
            return RecordKey == r.RecordKey
                && PluginID == r.PluginID
                && Query == r.OriginQuery.RawQuery;
        }
    }
}
