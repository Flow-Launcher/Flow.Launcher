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
        internal static IEnumerable<SearchResult> TopLevelDirectorySearch(Query query, string search, CancellationToken token)
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

        private static IEnumerable<SearchResult> DirectorySearch(EnumerationOptions enumerationOption, Query query, string search,
            string searchCriteria, CancellationToken token)
        {
            var results = new List<SearchResult>();

            var path = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(search);

            try
            {
                var directoryInfo = new System.IO.DirectoryInfo(path);

                foreach (var fileSystemInfo in directoryInfo.EnumerateFileSystemInfos(searchCriteria, enumerationOption))
                {
                    if (fileSystemInfo is System.IO.DirectoryInfo)
                    {
                        results.Add(new SearchResult()
                        {
                            FullPath = fileSystemInfo.FullName,
                            Type = ResultType.Folder,
                            WindowsIndexed = false
                        });
                    }
                    else
                    {
                        results.Add(new SearchResult()
                        {
                            FullPath = fileSystemInfo.FullName,
                            Type = ResultType.File,
                            WindowsIndexed = false
                        });
                    }

                    token.ThrowIfCancellationRequested();
                }
            }
            catch (Exception e)
            {
                Log.Exception("Flow.Plugin.Explorer.", nameof(DirectoryInfoSearch), e);
                throw;
            }

            // Initial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results.OrderBy(r=>r.Type).ThenBy(r=>r.FullPath);
        }
    }
}