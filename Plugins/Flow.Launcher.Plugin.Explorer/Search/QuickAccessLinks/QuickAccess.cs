using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    internal static class QuickAccess
    {
        private const int quickAccessResultScore = 100;

        internal static List<Result> AccessLinkListMatched(Query query, List<AccessLink> accessLinks)
        {
            if (string.IsNullOrEmpty(query.Search))
                return new List<Result>();

            string search = query.Search.ToLower();

            var queriedAccessLinks =
                accessLinks
                .Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name);

            return queriedAccessLinks.Select(l => l.Type switch
            {
                ResultType.Folder => ResultManager.CreateFolderResult(l.Name, l.Path, l.Path, query, quickAccessResultScore),
                ResultType.File => ResultManager.CreateFileResult(l.Path, query, quickAccessResultScore),
                _ => throw new ArgumentOutOfRangeException()
            }).ToList();
        }

        internal static List<Result> AccessLinkListAll(Query query, List<AccessLink> accessLinks)
            => accessLinks
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .Select(l => l.Type switch
                {
                    ResultType.Folder => ResultManager.CreateFolderResult(l.Name, l.Path, l.Path, query),
                    ResultType.File => ResultManager.CreateFileResult(l.Path, query, quickAccessResultScore),
                    _ => throw new ArgumentOutOfRangeException()
                }).ToList();
    }
}
