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
                search.LastIndexOf(Constants.DirectorySeparator))
                return DirectorySearch(new EnumerationOptions
                {
                    RecurseSubdirectories = true
                }, search, criteria, token);

            return DirectorySearch(new EnumerationOptions(), search, criteria,
                token); // null will be passed as default
        }

        public static string ConstructSearchCriteria(string search)
        {
            string incompleteName = "";

            if (!search.EndsWith(Constants.DirectorySeparator))
            {
                var indexOfSeparator = search.LastIndexOf(Constants.DirectorySeparator);

                incompleteName = search[(indexOfSeparator + 1)..].ToLower();

                if (incompleteName.StartsWith(Constants.AllFilesFolderSearchWildcard))
                    incompleteName = string.Concat("*", incompleteName.AsSpan(1));
            }

            incompleteName += "*";

            return incompleteName;
        }

        private static IEnumerable<SearchResult> DirectorySearch(EnumerationOptions enumerationOption, string search,
            string searchCriteria, CancellationToken token)
        {
            var results = new List<SearchResult>();

            var path = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(search);

            try
            {
                var directoryInfo = new System.IO.DirectoryInfo(path);

                foreach (var fileSystemInfo in directoryInfo.EnumerateFileSystemInfos(searchCriteria, enumerationOption))
                {
                    results.Add(new SearchResult
                    {
                        FullPath = fileSystemInfo.FullName,
                        Type = fileSystemInfo switch
                        {
                            System.IO.DirectoryInfo {Parent: null} => ResultType.Volume,
                            System.IO.DirectoryInfo => ResultType.Folder,
                            FileInfo => ResultType.File,
                            _ => throw new ArgumentOutOfRangeException(nameof(fileSystemInfo))
                        },
                        WindowsIndexed = false
                    });

                    if (token.IsCancellationRequested)
                        return results;
                }
            }
            catch (Exception e)
            {
                Log.Exception(nameof(DirectoryInfoSearch), "Error occurred while searching path", e);
                
                throw;
            }

            // Initial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results.OrderBy(r=>r.Type).ThenBy(r=>r.FullPath);
        }
    }
}
