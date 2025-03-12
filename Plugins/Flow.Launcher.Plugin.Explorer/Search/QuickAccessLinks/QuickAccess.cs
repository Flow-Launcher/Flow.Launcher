using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    internal static class QuickAccess
    {
        private const int quickAccessResultScore = 100;

        internal static List<Result> AccessLinkListMatched(Query query, IEnumerable<AccessLink> accessLinks)
        {
            if (string.IsNullOrEmpty(query.Search))
                return new List<Result>();

            return accessLinks
                .Where(x => Main.Context.API.FuzzySearch(query.Search, x.Name).IsSearchPrecisionScoreMet() || Main.Context.API.FuzzySearch(query.Search, x.Path).IsSearchPrecisionScoreMet())
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .Select(l => l.Type switch
                    {
                        ResultType.Folder => ResultManager.CreateFolderResult(l.Name, l.Path, l.Path, query, quickAccessResultScore),
                        ResultType.File => ResultManager.CreateFileResult(l.Path, query, quickAccessResultScore),
                        _ => throw new ArgumentOutOfRangeException()
                    })
                .ToList();
        }

        internal static List<Result> AccessLinkListAll(Query query, IEnumerable<AccessLink> accessLinks)
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
