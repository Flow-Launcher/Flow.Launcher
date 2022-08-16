using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo
{
    public static class DirectoryInfoSearch
    {
        internal static List<Result> TopLevelDirectorySearch(Query query, string search, CancellationToken token)
        {
            var criteria = ConstructSearchCriteria(search);

            if (search.LastIndexOf(Constants.AllFilesFolderSearchWildcard) >
                search.LastIndexOf(Constants.DirectorySeperator))
                return DirectorySearch(new EnumerationOptions
                {
                    RecurseSubdirectories = true
                }, query, search, criteria, token);

            return DirectorySearch(new EnumerationOptions(), query, search, criteria,
                token); // null will be passed as default
        }

        public static string ConstructSearchCriteria(string search)
        {
            string incompleteName = "";

            if (!search.EndsWith(Constants.DirectorySeperator))
            {
                var indexOfSeparator = search.LastIndexOf(Constants.DirectorySeperator);

                incompleteName = search.Substring(indexOfSeparator + 1).ToLower();

                if (incompleteName.StartsWith(Constants.AllFilesFolderSearchWildcard))
                    incompleteName = "*" + incompleteName.Substring(1);
            }

            incompleteName += "*";

            return incompleteName;
        }

        private static List<Result> DirectorySearch(EnumerationOptions enumerationOption, Query query, string search,
            string searchCriteria, CancellationToken token)
        {
            var results = new List<Result>();

            var path = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(search);

            var folderList = new List<Result>();
            var fileList = new List<Result>();

            try
            {
                var directoryInfo = new System.IO.DirectoryInfo(path);

                foreach (var fileSystemInfo in directoryInfo.EnumerateFileSystemInfos(searchCriteria, enumerationOption))
                {
                    if (fileSystemInfo is System.IO.DirectoryInfo)
                    {
                        folderList.Add(ResultManager.CreateFolderResult(fileSystemInfo.Name, fileSystemInfo.FullName,
                            fileSystemInfo.FullName, query, 0, true, false));
                    }
                    else
                    {
                        fileList.Add(ResultManager.CreateFileResult(fileSystemInfo.FullName, query, 0, true, false));
                    }

                    token.ThrowIfCancellationRequested();
                }
            }
            catch (Exception e)
            {
                Log.Exception(nameof(DirectoryInfoSearch), "Error occured while searching path", e);
                
                results.Add(
                    new Result
                    {
                        Title = string.Format(SearchManager.Context.API.GetTranslation(
                                                "plugin_explorer_directoryinfosearch_error"),
                                                e.Message),
                        Score = 501, 
                        IcoPath = Constants.ExplorerIconImagePath
                    });

                return results;
            }

            // Initial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results.Concat(folderList.OrderBy(x => x.Title)).Concat(fileList.OrderBy(x => x.Title)).ToList();
        }
    }
}