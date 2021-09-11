using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin
{
    public class Query
    {
        public Query() { }

        /// <summary>
        /// to allow unit tests for plug ins
        /// </summary>
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
        /// Search part of a query.
        /// This will not include action keyword if exclusive plugin gets it, otherwise it should be same as RawQuery.
        /// Since we allow user to switch a exclusive plugin to generic plugin, 
        /// so this property will always give you the "real" query part of the query
        /// </summary>
        public string Search { get; internal init; }

        /// <summary>
        /// The search string split into a string array.
        /// </summary>
        public string[] SearchTerms { get; init; }
        
        /// <summary>
        /// The raw query split into a string array
        /// </summary>
        [Obsolete("It may or may not include action keyword, which can be confusing. Use SearchTerms instead")]
        public string[] Terms { get; init; }

        /// <summary>
        /// Query can be splited into multiple terms by whitespace
        /// </summary>
        public const string TermSeparator = " ";

        [Obsolete("Typo")]
        public const string TermSeperater = TermSeparator;
        /// <summary>
        /// User can set multiple action keywords seperated by ';'
        /// </summary>
        public const string ActionKeywordSeparator = ";";
        
        [Obsolete("Typo")]
        public const string ActionKeywordSeperater = ActionKeywordSeparator;


        /// <summary>
        /// '*' is used for System Plugin
        /// </summary>
        public const string GlobalPluginWildcardSign = "*";

        public string ActionKeyword { get; init; }

        /// <summary>
        /// Return first search split by space if it has
        /// </summary>
        public string FirstSearch => SplitSearch(0);

        private string _secondToEndSearch;

        /// <summary>
        /// strings from second search (including) to last search
        /// </summary>
        public string SecondToEndSearch => _secondToEndSearch ??= string.Join(' ', SearchTerms[1..]);

        /// <summary>
        /// Return second search split by space if it has
        /// </summary>
        public string SecondSearch => SplitSearch(1);

        /// <summary>
        /// Return third search split by space if it has
        /// </summary>
        public string ThirdSearch => SplitSearch(2);

        private string SplitSearch(int index)
        {
            return index < SearchTerms.Length ? SearchTerms[index] : string.Empty;
        }

        public override string ToString() => RawQuery;
    }
}