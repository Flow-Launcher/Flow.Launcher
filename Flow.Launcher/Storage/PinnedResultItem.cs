using System;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

/// <summary>
/// A serializable result used to record the pinned results.
/// Inherits common result fields from <see cref="Result"/> and adds the original query and execution time.
/// </summary>
public class PinnedResultItem : Result
{
    /// <summary>
    /// Gets or sets the date and time when the item was last pinned.
    /// </summary>
    public DateTime LastPinnedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current operation is a query.
    /// </summary>
    public bool IsQuery { get; set; }

    /// <summary>
    /// The query string from Query.TrimmedQuery property, it is stored as a string instead of the entire Query class <see cref="Result"/>. 
    /// This is used so results can be reopened or re-run using the serialized query string.
    /// </summary>
    public string Query { get; set;  }

    /// <summary>
    /// Initializes a new instance of <see cref="PinnedResultItem"/>.
    /// </summary>
    public PinnedResultItem()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinnedResultItem"/> using the specified result and query string.
    /// </summary>
    /// <param name="result">The result object containing the data to be pinned. Cannot be null.</param>
    /// <param name="query">The query string associated with the pinned result. If null, an empty string is used.</param>
    public PinnedResultItem(Result result, string query)
    {
        Title = result.Title;
        SubTitle = result.SubTitle;
        PluginID = result.PluginID;
        OriginQuery = result.OriginQuery;
        RecordKey = result.RecordKey;
        IcoPath = result.IcoPath;
        PluginDirectory = result.PluginDirectory;
        Glyph = result.Glyph;
        ShowBadge = result.ShowBadge;
        BadgeIcoPath = result.BadgeIcoPath;
        RoundedIcon = result.RoundedIcon;
        Score = result.Score;
        TitleHighlightData = result.TitleHighlightData;
        CopyText = result.CopyText;
        AutoCompleteText = result.AutoCompleteText;
        LastPinnedAt = DateTime.Now;
        Query = query ?? string.Empty;
        IsQuery = !string.IsNullOrEmpty(query);
        AsyncAction = null;
    }

    /// <summary>
    /// Creates a deep copy of the current PinnedResultItem instance, duplicating its properties and associated data.
    /// </summary>
    /// <remarks>The deep copy includes all relevant fields, ensuring that mutable objects and references are
    /// duplicated where necessary. This method is useful when a separate instance is required for further manipulation
    /// without altering the original item.</remarks>
    /// <returns>A new PinnedResultItem object that contains copies of the original instance's properties. The returned object is
    /// independent of the source and modifications to it will not affect the original.</returns>
    public PinnedResultItem DeepCopy()
    {
        var queryValue = Query;
        var glyphValue = Glyph;

        var title = string.Empty;
        var subtitle = string.Empty;
        var icoPath = string.Empty;
        var glyph = null as GlyphInfo;

        if (IsQuery)
        {
            title = Localize.executeQuery(Query);
            subtitle = Localize.lastPinnedAt(LastPinnedAt);
            icoPath = Constant.HistoryIcon;
            glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE81C");
        }
        else
        {
            title = Title;
            subtitle = SubTitle;
            icoPath = IcoPath;
            glyph = glyphValue != null
                        ? new GlyphInfo(glyphValue.FontFamily, glyphValue.Glyph)
                        : null;
        }

        return new PinnedResultItem()
        {
            Title = title,
            SubTitle = subtitle,
            PluginID = PluginID,
            Query = Query,
            OriginQuery = new Query { TrimmedQuery = Query },
            RecordKey = RecordKey,
            IcoPath = icoPath,
            ShowBadge = ShowBadge,
            BadgeIcoPath = BadgeIcoPath,
            PluginDirectory = PluginDirectory,
            RoundedIcon = RoundedIcon,
            Score = Score,
            TitleHighlightData = TitleHighlightData,
            CopyText = CopyText,
            AutoCompleteText = AutoCompleteText,
            Action = _ =>
            {
                App.API.BackToQueryResults();
                App.API.ChangeQuery(queryValue);
                return false;
            },
            IsQuery = IsQuery,
            AsyncAction = null,
            Glyph = glyph,
            LastPinnedAt = LastPinnedAt
        };
    }

    /// <summary>
    /// Determines whether the current Result instance is equal to the specified Result based on key properties and
    /// query state.
    /// </summary>
    /// <remarks>Equality is determined by comparing either the RecordKey or, if RecordKey is null or empty,
    /// the Title and SubTitle properties, along with PluginID, IsQuery, and the trimmed OriginQuery. The comparison
    /// ignores internal implementation details and focuses on exposed properties relevant to equality.</remarks>
    /// <param name="r">The Result instance to compare with the current instance. Must not be null.</param>
    /// <returns>true if the specified Result is considered equal to the current instance; otherwise, false.</returns>
    public bool Equals(Result r)
    {
        if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
        {
            return Title == r.Title
                && SubTitle == r.SubTitle
                && PluginID == r.PluginID
                && IsQuery == false;
                //&& Query == r.OriginQuery.TrimmedQuery;
        }
        else
        {
            return RecordKey == r.RecordKey
                && PluginID == r.PluginID
                && IsQuery == false;
                //&& Query == r.OriginQuery.TrimmedQuery;
        }
    }

    /// <summary>
    /// Determines whether the current result matches the specified result and query based on key and plugin
    /// information.
    /// </summary>
    /// <remarks>The comparison uses either record keys or, if keys are missing, title and subtitle fields.
    /// The method also requires that both results are associated with a query and the same plugin.</remarks>
    /// <param name="r">The result to compare with the current instance. Must not be null.</param>
    /// <param name="query">The query string to match against the result. Cannot be null.</param>
    /// <returns>true if the results are considered equal according to their keys, plugin IDs, and query; otherwise, false.</returns>
    public bool Equals(Result r, string query)
    {
        if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
        {
            return Title == r.Title
                && SubTitle == r.SubTitle
                && PluginID == r.PluginID
                && IsQuery == true
                && Query == query;
        }
        else
        {
            return RecordKey == r.RecordKey
                && PluginID == r.PluginID
                && IsQuery == true
                && Query == query;
        }
    }
}
