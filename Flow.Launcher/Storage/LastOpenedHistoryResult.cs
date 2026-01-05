using System;
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
    /// Selectively creates a deep copy of the required properties for <see cref="LastOpenedHistoryResult"/>.
    /// This copy should be independent of original and full isolated.
    /// </summary>
    /// <returns>A new <see cref="LastOpenedHistoryResult"/> containing the same required data.</returns>
    public LastOpenedHistoryResult DeepCopy()
    {
        // queryValue and glyphValue are captured to ensure they are correctly referenced in the Action delegate.
        var queryValue = Query;
        var glyphValue = Glyph;
        return new LastOpenedHistoryResult
        {
            Title = Title,
            SubTitle = SubTitle,
            PluginID = PluginID,
            Query = Query,
            OriginQuery = new Query { TrimmedQuery = Query },
            RecordKey = RecordKey,
            IcoPath = IcoPath,
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
            Glyph = glyphValue != null 
                        ? new GlyphInfo(glyphValue.FontFamily, glyphValue.Glyph)
                        : null,
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
