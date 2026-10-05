using System;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

/// <summary>
/// A serializable result used to record the last opened history for reopening results.
/// Inherits common result fields from <see cref="Result"/> and adds the original query and execution time.
/// </summary>
public class LastOpenedHistoryResult : Result
{
    /// <summary>
    /// The query string from Query.TrimmedQuery property, it is stored as a string instead of the entire Query class <see cref="Result"/>. 
    /// This is used so results can be reopened or re-run using the serialized query string.
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// The local date and time when this result was executed/opened.
    /// </summary>
    public DateTime ExecutedDateTime { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="LastOpenedHistoryResult"/>.
    /// </summary>
    public LastOpenedHistoryResult()
    {
    }

    /// <summary>
    /// Creates a <see cref="LastOpenedHistoryResult"/> from an existing <see cref="Result"/>.
    /// Copies required fields and sets up default reopening actions.
    /// </summary>
    /// <param name="result">The original result to create history from.</param>
    public LastOpenedHistoryResult(Result result)
    {
        Title = result.Title;
        SubTitle = result.SubTitle;
        PluginID = result.PluginID;
        Query = result.OriginQuery.TrimmedQuery;
        OriginQuery = result.OriginQuery;
        RecordKey = result.RecordKey;
        IcoPath = result.IcoPath;
        PluginDirectory = result.PluginDirectory;
        Glyph = result.Glyph;
        ExecutedDateTime = DateTime.Now;
        // Used for Query History style reopening
        Action = _ =>
        {
            App.API.BackToQueryResults();
            App.API.ChangeQuery(result.OriginQuery.TrimmedQuery);
            return false;
        };
        // Used for Last Opened History style reopening, currently need to be assigned at MainViewModel.cs
        AsyncAction = null;
    }

    /// <summary>
    /// Selectively creates a deep copy of the required properties for <see cref="LastOpenedHistoryResult"/>
    /// based on the style of history- Last Opened or Query.
    /// This copy should be independent of original and full isolated.
    /// </summary>
    /// <returns>A new <see cref="LastOpenedHistoryResult"/> containing the same required data.</returns>
    public LastOpenedHistoryResult DeepCopyForHistoryStyle(bool isHistoryStyleLastOpened)
    {
        // queryValue and glyphValue are captured to ensure they are correctly referenced in the Action delegate.
        var queryValue = Query;
        var glyphValue = Glyph;

        var title = string.Empty;
        var showBadge = false;
        var badgeIcoPath = string.Empty;
        var icoPath = string.Empty;
        var glyph = null as GlyphInfo;

        if (isHistoryStyleLastOpened)
        {
            title = Title;
            icoPath = IcoPath;
            glyph = glyphValue != null
                        ? new GlyphInfo(glyphValue.FontFamily, glyphValue.Glyph)
                        : null;
            showBadge = true;
            badgeIcoPath = Constant.HistoryIcon;
        }
        else
        {
            title = Localize.executeQuery(Query);
            icoPath = Constant.HistoryIcon;
            glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE81C");
            showBadge = false;
        }

        return new LastOpenedHistoryResult
        {
            Title = title,
            // Subtitle has datetime which can cause duplicates when saving.
            SubTitle = Localize.lastExecuteTime(ExecutedDateTime),
            // Empty PluginID so the source of last opened history results won't be updated, this copy is meant to be temporary.
            PluginID = string.Empty,
            Query = Query,
            OriginQuery = new Query { TrimmedQuery = Query },
            RecordKey = RecordKey,
            IcoPath = icoPath,
            ShowBadge = showBadge,
            BadgeIcoPath = badgeIcoPath,
            PluginDirectory = PluginDirectory,
            // Used for Query History style reopening
            Action = _ =>
            {
                App.API.BackToQueryResults();
                App.API.ChangeQuery(queryValue);
                return false;
            },
            // Used for Last Opened History style reopening, currently need to be assigned at MainViewModel.cs
            AsyncAction = null,
            Glyph = glyph,
            ExecutedDateTime = ExecutedDateTime
            // Note: Other properties are left as default — copy if needed.
        };
    }

    /// <summary>
    /// Determines whether the specified <see cref="Result"/> is equivalent to this history result.
    /// Comparison uses <see cref="Result.RecordKey"/> when available; otherwise falls back to title/subtitle/plugin id and query.
    /// </summary>
    /// <param name="r">The result to compare to.</param>
    /// <returns><c>true</c> if the results are considered equal; otherwise <c>false</c>.</returns>
    public bool Equals(Result r)
    {
        if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
        {
            return Title == r.Title
                && SubTitle == r.SubTitle
                && PluginID == r.PluginID
                && Query == r.OriginQuery.TrimmedQuery;
        }
        else
        {
            return RecordKey == r.RecordKey
                && PluginID == r.PluginID
                && Query == r.OriginQuery.TrimmedQuery;
        }
    }
}
