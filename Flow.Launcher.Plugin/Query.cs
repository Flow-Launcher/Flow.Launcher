using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    public class Query
    {
        public Query() { }

        [Obsolete("Use the default Query constructor.")]
        public Query(string rawQuery, string search, string[] terms, string[] searchTerms, string actionKeyword = "")
        {
            Search = search;
            RawQuery = rawQuery;
            SearchTerms = searchTerms;
            ActionKeyword = actionKeyword;
        }

        /// <summary>
        /// Raw query, this includes action keyword if it has
        /// We didn't recommend use this property directly. You should always use Search property.
        /// </summary>
        public string RawQuery { get; internal init; }

        /// <summary>
        /// Determines whether the query was forced to execute again.
        /// For example, the value will be true when the user presses Ctrl + R.
        /// When this property is true, plugins handling this query should avoid serving cached results.
        /// </summary>
        public bool IsReQuery { get; internal set; } = false;

        /// <summary>
        /// Search part of a query.
        /// This will not include action keyword if exclusive plugin gets it, otherwise it should be same as RawQuery.
        /// Since we allow user to switch a exclusive plugin to generic plugin,
        /// so this property will always give you the "real" query part of the query
        /// </summary>
        public string Search { get; internal init; }

        /// <summary>
        /// The search string split into a string array.
        /// Does not include the <see cref="ActionKeyword"/>.
        /// </summary>
        public string[] SearchTerms { get; init; }

        /// <summary>
        /// Query can be splited into multiple terms by whitespace
        /// </summary>
        public const string TermSeparator = " ";

        /// <summary>
        /// User can set multiple action keywords seperated by ';'
        /// </summary>
        public const string ActionKeywordSeparator = ";";


        /// <summary>
        /// Wildcard action keyword. Plugins using this value will be queried on every search.
        /// </summary>
        public const string GlobalPluginWildcardSign = "*";

        /// <summary>
        /// The action keyword part of this query.
        /// For global plugins this value will be empty.
        /// </summary>
        public string ActionKeyword { get; init; }

        [JsonIgnore]
        /// <summary>
        /// Splits <see cref="SearchTerms"/> by spaces and returns the first item.
        /// </summary>
        /// <remarks>
        /// returns an empty string when <see cref="SearchTerms"/> does not have enough items.
        /// </remarks>
        public string FirstSearch => SplitSearch(0);
        
        [JsonIgnore]
        private string _secondToEndSearch;
        
        /// <summary>
        /// strings from second search (including) to last search
        /// </summary>
        [JsonIgnore]
        public string SecondToEndSearch => SearchTerms.Length > 1 ? (_secondToEndSearch ??= string.Join(' ', SearchTerms[1..])) : "";

        /// <summary>
        /// Splits <see cref="SearchTerms"/> by spaces and returns the second item.
        /// </summary>
        /// <remarks>
        /// returns an empty string when <see cref="SearchTerms"/> does not have enough items.
        /// </remarks>
        [JsonIgnore]
        public string SecondSearch => SplitSearch(1);

        /// <summary>
        /// Splits <see cref="SearchTerms"/> by spaces and returns the third item.
        /// </summary>
        /// <remarks>
        /// returns an empty string when <see cref="SearchTerms"/> does not have enough items.
        /// </remarks>
        [JsonIgnore]
        public string ThirdSearch => SplitSearch(2);

        private string SplitSearch(int index)
        {
            return index < SearchTerms.Length ? SearchTerms[index] : string.Empty;
        }

        /// <inheritdoc />
        public override string ToString() => RawQuery;
    }
}
