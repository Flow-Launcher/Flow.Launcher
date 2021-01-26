using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    public class QuickAccess
    {
        private readonly ResultManager resultManager;

        public QuickAccess(PluginInitContext context)
        {
            resultManager = new ResultManager(context);
        }

        internal List<Result> AccessLinkListMatched(Query query, List<AccessLink> accessLinks)
        {
            if (string.IsNullOrEmpty(query.Search))
                return new List<Result>();

            string search = query.Search.ToLower();

            var queriedAccessLinks =
                accessLinks
                .Where(x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Nickname);

            return queriedAccessLinks
                .Where(x => x.Type == ResultType.Folder)
                .Select(item => 
                    resultManager.CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                .Concat(
                queriedAccessLinks
                .Where(x => x.Type == ResultType.File)
                .Select(item =>
                    resultManager.CreateFileResult(item.Path, query)))
                .ToList();
        }

        internal List<Result> AccessLinkListAll(Query query, List<AccessLink> accessLinks)
            => accessLinks
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Nickname)
                .Where(x => x.Type == ResultType.Folder)
                .Select(item => 
                    resultManager.CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                .Concat(
                accessLinks
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Nickname)
                .Where(x => x.Type == ResultType.File)
                .Select(item =>
                    resultManager.CreateFileResult(item.Path, query)))
                .ToList();
    }
}
