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
                accessLinks.Where(x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));

            return queriedAccessLinks
                .Where(x => x.Type == ResultType.Folder)
                .Select(item => 
                    resultManager.CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                    .OrderBy(x => x.Title)
                .Concat(
                queriedAccessLinks
                .Where(x => x.Type == ResultType.File)
                .Select(item =>
                    resultManager.CreateFileResult(item.Path, query))
                    .OrderBy(x => x.Title))
                .ToList();
        }

        internal List<Result> AccessLinkListAll(Query query, List<AccessLink> accessLinks)
            => accessLinks
                .Where(x => x.Type == ResultType.Folder)
                .Select(item => 
                    resultManager.CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                    .OrderBy(x => x.Title)
                .Concat(
                accessLinks
                .Where(x => x.Type == ResultType.File)
                .Select(item =>
                    resultManager.CreateFileResult(item.Path, query))
                    .OrderBy(x => x.Title))
                .ToList();
    }
}
