using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.FolderLinks
{
    public class QuickFolderAccess
    {
        internal List<Result> FolderListMatched(Query query, List<FolderLink> folderLinks, PluginInitContext context)
        {
            if (string.IsNullOrEmpty(query.Search))
                return new List<Result>();

            string search = query.Search.ToLower();
            
            var queriedFolderLinks = folderLinks.Where(x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));

            return queriedFolderLinks.Select(item =>
                                                new ResultManager(context)
                                                        .CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                                     .ToList();
        }

        internal List<Result> FolderListAll(Query query, List<FolderLink> folderLinks, PluginInitContext context)
            => folderLinks
                .Select(item => 
                            new ResultManager(context).CreateFolderResult(item.Nickname, item.Path, item.Path, query))
                .ToList();
    }
}
