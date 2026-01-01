using System;
using System.DirectoryServices.ActiveDirectory;
using Flow.Launcher.Helper;
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
    /// The UTC date and time when this result was executed/opened.
    /// </summary>
    public DateTime ExecutedDateTime { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="LastOpenedHistoryResult"/>.
    /// Sets <see cref="OriginQuery"/> using the current <see cref="Query"/> value, because OriginQuery is not serialized.
    /// </summary>
    public LastOpenedHistoryResult()
    {
        this.OriginQuery = new Query { TrimmedQuery = Query };
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
        //Used for Last Opened History style reopening, currently need to be assigned at MainViewModel.cs
        AsyncAction = null;
    }

    /// <summary>
    /// Creates a deep copy of <see cref="LastOpenedHistoryResult"/>.
    /// </summary>
    /// <returns>A new <see cref="LastOpenedHistoryResult"/> containing the same required data.</returns>
    public LastOpenedHistoryResult Copy()
    {
        
        return new LastOpenedHistoryResult
        {
            Title = this.Title,
            SubTitle = this.SubTitle,
            PluginID = this.PluginID,
            Query = this.Query,
            OriginQuery = this.OriginQuery,
            RecordKey = this.RecordKey,
            IcoPath = this.IcoPath,
            PluginDirectory = this.PluginDirectory,
            Action = this.Action,
            AsyncAction = this.AsyncAction,
            Glyph = this.Glyph,
            ExecutedDateTime = this.ExecutedDateTime
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
