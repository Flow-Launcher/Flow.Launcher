using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo
{
    public class DirectoryInfoSearch
    {
        private PluginInitContext _context;

        private Settings _settings;

        public DirectoryInfoSearch(Settings settings, PluginInitContext context)
        {
            _context = context;
            _settings = settings;
        }
        public List<Result> TopLevelDirectorySearch(Query query, string search)
        {
            var results = new List<Result>();
            //var hasSpecial = search.IndexOfAny(_specialSearchChars) >= 0;
            string incompleteName = "";
            //if (hasSpecial || !Directory.Exists(search + "\\"))
            if (!FilesFolders.LocationExists(search + "\\"))
            {
                // if folder doesn't exist, we want to take the last part and use it afterwards to help the user 
                // find the right folder.
                int index = search.LastIndexOf('\\');
                if (index > 0 && index < (search.Length - 1))
                {
                    incompleteName = search.Substring(index + 1).ToLower();
                    search = search.Substring(0, index + 1);
                    if (!FilesFolders.LocationExists(search))
                    {
                        return results;
                    }
                }
                else
                {
                    return results;
                }
            }
            else
            {
                // folder exist, add \ at the end of doesn't exist
                if (!search.EndsWith("\\"))
                {
                    search += "\\";
                }
            }

            results.Add(new ResultManager().CreateOpenCurrentFolderResult(incompleteName, search));

            var searchOption = SearchOption.TopDirectoryOnly;
            incompleteName += "*";

            //// give the ability to search all folder when starting with >
            //if (incompleteName.StartsWith(">"))
            //{
            //    searchOption = SearchOption.AllDirectories;

            //    // match everything before and after search term using supported wildcard '*', ie. *searchterm*
            //    incompleteName = "*" + incompleteName.Substring(1);
            //}

            var folderList = new List<Result>();
            var fileList = new List<Result>();

            var folderSubtitleString = Constants.DefaultFolderSubtitleString;

            try
            {
                // search folder and add results
                var directoryInfo = new System.IO.DirectoryInfo(search);
                var fileSystemInfos = directoryInfo.GetFileSystemInfos(incompleteName, searchOption);

                foreach (var fileSystemInfo in fileSystemInfos)
                {
                    if ((fileSystemInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                    if (fileSystemInfo is System.IO.DirectoryInfo)
                    {
                        if (searchOption == SearchOption.AllDirectories)
                            folderSubtitleString = fileSystemInfo.FullName;

                        folderList.Add(new ResultManager().CreateFolderResult(fileSystemInfo.Name, folderSubtitleString, fileSystemInfo.FullName, query));
                    }
                    else
                    {
                        fileList.Add(new ResultManager().CreateFileResult(fileSystemInfo.FullName, query));
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

                throw;
            }

            // Intial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results.Concat(folderList.OrderBy(x => x.Title)).Concat(fileList.OrderBy(x => x.Title)).ToList(); //<===== MOVE ORDERING OUT
        }
    }
}
