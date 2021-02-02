using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    internal static class QuickAccess
    {
        internal static List<Result> AccessLinkListMatched(Query query, List<AccessLink> accessLinks)
        {
            if (string.IsNullOrEmpty(query.Search))
                return new List<Result>();

            string search = query.Search.ToLower();

            var queriedAccessLinks =
                accessLinks
                .Where(x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Nickname);

            return queriedAccessLinks.Select(l => l.Type switch
            {
                ResultType.Folder => ResultManager.CreateFolderResult(l.Nickname, l.Path, l.Path, query),
                ResultType.File => ResultManager.CreateFileResult(l.Path, query),
                _ => throw new ArgumentOutOfRangeException()

            }).ToList();
        }

        internal static List<Result> AccessLinkListAll(Query query, List<AccessLink> accessLinks)
            => accessLinks
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Nickname)
                .Select(l => l.Type switch
                {
                    ResultType.Folder => ResultManager.CreateFolderResult(l.Nickname, l.Path, l.Path, query),
                    ResultType.File => ResultManager.CreateFileResult(l.Path, query),
                    _ => throw new ArgumentOutOfRangeException()

                }).ToList();
    }
}
