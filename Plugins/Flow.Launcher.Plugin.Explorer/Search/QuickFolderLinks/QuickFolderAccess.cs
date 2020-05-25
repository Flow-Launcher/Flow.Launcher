using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickFolderLinks
{
    public class QuickFolderAccess
    {
        internal List<Result> FolderList(Query query, List<FolderLink> folderLinks)
        {
            string search = query.Search.ToLower();
            var userFolderLinks = folderLinks.Where(
                x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));
            var results = userFolderLinks.Select(item =>
                ResultManager.CreateFolderResult(item.Nickname, Constants.DefaultFolderSubtitleString, item.Path, query)).ToList();
            return results;
        }
    }
}
