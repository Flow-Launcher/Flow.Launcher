using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.Storage;

public class History
{
    public List<LastOpenedHistoryResult> LastOpenedHistoryItems { get; set; } = [];

    private readonly int _maxHistory = 300;

    public void Add(string queryText, Result result)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return;
        }

        if (string.IsNullOrEmpty(result.PluginID))
        {
            return;
        }

        var existingHistoryItem = LastOpenedHistoryItems.FirstOrDefault(x => x.Equals(queryText, result));
        if (existingHistoryItem is not null)
        {
            existingHistoryItem.ExecutedDateTime = DateTime.Now;
            existingHistoryItem.Query = queryText;
            existingHistoryItem.UpdateIcon(result);
            return;
        }

        if (LastOpenedHistoryItems.Count >= _maxHistory)
        {
            LastOpenedHistoryItems.RemoveAt(0);
        }

        LastOpenedHistoryItems.Add(new LastOpenedHistoryResult(queryText, result));
    }

    /// <summary>
    /// Removes an item from history. With <paramref name="removeAllMatchingResults"/> every entry representing the same
    /// result (regardless of query) is removed, as used by the last-opened history style.
    /// </summary>
    /// <returns>The number of removed history entries.</returns>
    public int Remove(LastOpenedHistoryResult historyItem, bool removeAllMatchingResults = false)
    {
        if (!removeAllMatchingResults)
        {
            return LastOpenedHistoryItems.Remove(historyItem) ? 1 : 0;
        }

        return LastOpenedHistoryItems.RemoveAll(item =>
            item.Title == historyItem.Title
            && item.SubTitle == historyItem.SubTitle
            && item.PluginID == historyItem.PluginID
            && item.RecordKey == historyItem.RecordKey);
    }
}

public class LastOpenedHistoryResult
{
    public string Title { get; set; } = string.Empty;

    public string SubTitle { get; set; } = string.Empty;

    public string PluginID { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public string RecordKey { get; set; } = string.Empty;

    public DateTime ExecutedDateTime { get; set; }

    public string IcoPath { get; set; } = string.Empty;

    public string PluginDirectory { get; set; } = string.Empty;

    public GlyphInfo? Glyph { get; set; }

    public LastOpenedHistoryResult()
    {
    }

    public LastOpenedHistoryResult(string queryText, Result result)
    {
        Title = result.Title ?? string.Empty;
        SubTitle = result.SubTitle ?? string.Empty;
        PluginID = result.PluginID ?? string.Empty;
        Query = queryText;
        RecordKey = result.RecordKey ?? string.Empty;
        ExecutedDateTime = DateTime.Now;
        IcoPath = result.IcoPath ?? string.Empty;
        PluginDirectory = result.PluginDirectory ?? string.Empty;
        Glyph = result.Glyph;
    }

    /// <summary>
    /// Keeps the stored icon in sync with the result that was just executed.
    /// </summary>
    public void UpdateIcon(Result result)
    {
        IcoPath = result.IcoPath ?? string.Empty;
        PluginDirectory = result.PluginDirectory ?? string.Empty;
        Glyph = result.Glyph;
    }

    public bool Equals(string queryText, Result result)
    {
        if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(result.RecordKey))
        {
            return Title == (result.Title ?? string.Empty)
                && SubTitle == (result.SubTitle ?? string.Empty)
                && PluginID == (result.PluginID ?? string.Empty)
                && Query == queryText;
        }

        return RecordKey == result.RecordKey
            && PluginID == (result.PluginID ?? string.Empty)
            && Query == queryText;
    }
}
