using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Query Build(string text, Dictionary<string, PluginPair> nonGlobalPlugins)
        {
            // replace multiple white spaces with one white space
            var textSplit = text.Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (textSplit.Length == 0)
            { // nothing was typed
                return null;
            }

            var rawQuery = string.Join(Query.TermSeparator, textSplit);
            string actionKeyword, search;
            string possibleActionKeyword = textSplit[0];
            string[] terms;

            if (nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var pluginPair) && !pluginPair.Metadata.Disabled)
            { // use non global plugin for query
                actionKeyword = possibleActionKeyword;
                search = textSplit.Length > 1 ? rawQuery[(actionKeyword.Length + 1)..] : string.Empty;
                terms = textSplit[1..];
            }
            else
            { // non action keyword
                actionKeyword = string.Empty;
                search = rawQuery;
                terms = textSplit;
            }

            var query = new Query(rawQuery, search, terms, actionKeyword);

            return query;
        }
    }
}