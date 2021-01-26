using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLink
{
    public class QuickAccess
    {
        private readonly ResultManager resultManager;

        public QuickAccess(PluginInitContext context)
        {
            resultManager = new ResultManager(context);
        }

        internal List<Result> FolderListMatched(Query query, List<AccessLink> folderLinks)
        {
            if (string.IsNullOrEmpty(query.Search))
                return new List<Result>();

            string search = query.Search.ToLower();

            var queriedFolderLinks =
                folderLinks.Where(x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));

            return queriedFolderLinks
                .Where(x => x.Type == ResultType.Folder)
                .Select(item => 
                    resultManager.CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                    .OrderBy(x => x.Title)
                .Concat(
                queriedFolderLinks
                .Where(x => x.Type == ResultType.File)
                .Select(item =>
                    resultManager.CreateFileResult(item.Path, query))
                    .OrderBy(x => x.Title))
                .ToList();
        }

        internal List<Result> FolderListAll(Query query, List<AccessLink> folderLinks)
            => folderLinks
                .Where(x => x.Type == ResultType.Folder)
                .Select(item => 
                    resultManager.CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                    .OrderBy(x => x.Title)
                .Concat(
                folderLinks
                .Where(x => x.Type == ResultType.File)
                .Select(item =>
                    resultManager.CreateFileResult(item.Path, query))
                    .OrderBy(x => x.Title))
                .ToList();
    }
}
