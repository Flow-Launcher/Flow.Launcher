using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Query Build(string originalQuery, string trimmedQuery, Dictionary<string, PluginPair> nonGlobalPlugins)
        {
            // home query
            if (string.IsNullOrEmpty(trimmedQuery))
            {
                return new Query()
                {
                    Search = string.Empty,
                    OriginalQuery = string.Empty,
                    TrimmedQuery = string.Empty,
                    SearchTerms = Array.Empty<string>(),
                    ActionKeyword = string.Empty,
                    IsHomeQuery = true
                };
            }

            // replace multiple white spaces with one white space
            var terms = trimmedQuery.Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                // nothing was typed
                return null;
            }

            string actionKeyword, search;
            string possibleActionKeyword = terms[0];
            string[] searchTerms;

            if (nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var pluginPair) && !pluginPair.Metadata.Disabled)
            {
                // use non global plugin for query
                actionKeyword = possibleActionKeyword;
                search = terms.Length > 1 ? trimmedQuery[(actionKeyword.Length + 1)..].TrimStart() : string.Empty;
                searchTerms = terms[1..];
            }
            else
            {
                // non action keyword
                actionKeyword = string.Empty;
                search = trimmedQuery.TrimStart();
                searchTerms = terms;
            }

            return new Query()
            {
                Search = search,
                OriginalQuery = originalQuery,
                TrimmedQuery = trimmedQuery,
                SearchTerms = searchTerms,
                ActionKeyword = actionKeyword,
                IsHomeQuery = false
            };
        }
    }
}
