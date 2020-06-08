using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo
{
    public class DirectoryInfoSearch
    {
        private readonly ResultManager resultManager;

        public DirectoryInfoSearch(PluginInitContext context)
        {
            resultManager = new ResultManager(context);
        }

        internal List<Result> TopLevelDirectorySearch(Query query, string search)
        {
            var criteria = ConstructSearchCriteria(search);

            if (search.LastIndexOf(Constants.AllFilesFolderSearchWildcard) > search.LastIndexOf(Constants.DirectorySeperator))
                return DirectorySearch(SearchOption.AllDirectories, query, search, criteria);
            
            return DirectorySearch(SearchOption.TopDirectoryOnly, query, search, criteria);
        }

        public string ConstructSearchCriteria(string search)
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

        private List<Result> DirectorySearch(SearchOption searchOption, Query query, string search, string searchCriteria)
        {
            var results = new List<Result>();

            var path = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(search);

            var folderList = new List<Result>();
            var fileList = new List<Result>();

            try
            {
                var directoryInfo = new System.IO.DirectoryInfo(path);
                var fileSystemInfos = directoryInfo.GetFileSystemInfos(searchCriteria, searchOption);

                foreach (var fileSystemInfo in fileSystemInfos)
                {
                    if ((fileSystemInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                    if (fileSystemInfo is System.IO.DirectoryInfo)
                    {
                        folderList.Add(resultManager.CreateFolderResult(fileSystemInfo.Name, Constants.DefaultFolderSubtitleString, fileSystemInfo.FullName, query, true, false));
                    }
                    else
                    {
                        fileList.Add(resultManager.CreateFileResult(fileSystemInfo.FullName, query, true, false));
                    }
                }
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException || e is ArgumentException)
                {
                    results.Add(new Result { Title = e.Message, Score = 501 });

                    return results;
                }

#if DEBUG // Please investigate and handle error from DirectoryInfo search
                throw e;
#else
                Log.Exception($"|Flow.Launcher.Plugin.Explorer.DirectoryInfoSearch|Error from performing DirectoryInfoSearch", e);
#endif          
            }

            // Intial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results.Concat(folderList.OrderBy(x => x.Title)).Concat(fileList.OrderBy(x => x.Title)).ToList();
        }
    }
}
