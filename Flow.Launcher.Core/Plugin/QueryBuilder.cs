using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Query Build(string input, string text, Dictionary<string, PluginPair> nonGlobalPlugins)
        {
            // home query
            if (string.IsNullOrEmpty(text))
            {
                return new Query()
                {
                    Search = string.Empty,
                    Input = string.Empty,
                    RawQuery = string.Empty,
                    SearchTerms = Array.Empty<string>(),
                    ActionKeyword = string.Empty,
                    IsHomeQuery = true
                };
            }

            // replace multiple white spaces with one white space
            var terms = text.Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                // nothing was typed
                return null;
            }

            var rawQuery = text;
            string actionKeyword, search;
            string possibleActionKeyword = terms[0];
            string[] searchTerms;

            if (nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var pluginPair) && !pluginPair.Metadata.Disabled)
            {
                // use non global plugin for query
                actionKeyword = possibleActionKeyword;
                search = terms.Length > 1 ? rawQuery[(actionKeyword.Length + 1)..].TrimStart() : string.Empty;
                searchTerms = terms[1..];
            }
            else
            {
                // non action keyword
                actionKeyword = string.Empty;
                search = rawQuery.TrimStart();
                searchTerms = terms;
            }

            return new Query()
            {
                Search = search,
                Input = input,
                RawQuery = rawQuery,
                SearchTerms = searchTerms,
                ActionKeyword = actionKeyword,
                IsHomeQuery = false
            };
        }
    }
}
