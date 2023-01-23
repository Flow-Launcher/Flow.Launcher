using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    internal static class QuickAccess
    {
        private const int quickAccessResultScore = 100;

        internal static IEnumerable<SearchResult> AccessLinkListMatched(Query query, IEnumerable<AccessLink> accessLinks)
        {
            if (string.IsNullOrEmpty(query.Search))
                return Enumerable.Empty<SearchResult>();

            string search = query.Search.ToLower();

            return accessLinks
                .Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .Select(x => new SearchResult()
                {
                    FullPath = x.Path,
                    Score = quickAccessResultScore,
                    Type = x.Type,
                    WindowsIndexed = false
                });
        }

        internal static IEnumerable<SearchResult> AccessLinkListAll(Query query, IEnumerable<AccessLink> accessLinks)
            => accessLinks
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .Select(l => new SearchResult()
                {
                    FullPath = l.Path,
                    Type = l.Type,
                    Score = quickAccessResultScore
                });
    }
}
