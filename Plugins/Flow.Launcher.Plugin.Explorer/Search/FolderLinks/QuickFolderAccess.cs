using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.FolderLinks
{
    public class QuickFolderAccess
    {
        internal List<Result> FolderList(Query query, List<FolderLink> folderLinks, PluginInitContext context)
        {
            if (string.IsNullOrEmpty(query.Search))
                return folderLinks
                        .Select(item =>
                                    new ResultManager(context)
                                            .CreateFolderResult(item.Nickname, Constants.DefaultFolderSubtitleString, item.Path, query))
                        .ToList();

            string search = query.Search.ToLower();
            
            var queriedFolderLinks = folderLinks.Where(x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));

            return queriedFolderLinks.Select(item =>
                                                new ResultManager(context)
                                                        .CreateFolderResult(item.Nickname, Constants.DefaultFolderSubtitleString, item.Path, query))
                                     .ToList();
        }
    }
}
