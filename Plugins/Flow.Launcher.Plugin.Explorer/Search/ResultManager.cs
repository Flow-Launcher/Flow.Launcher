using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    internal static class ResultManager
    {
        internal static Result CreateFolderResult(string title, string subtitle, string path, Query query, bool showIndexState = false, bool windowsIndexed = false)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = subtitle,
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                Action = c =>
                {
                    if (c.SpecialKeyState.CtrlPressed)
                    {
                        try
                        {
                            FilesFolders.OpenPath(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Could not start " + path);
                            return false;
                        }
                    }
                    
                    string changeTo = path.EndsWith(Constants.DirectorySeperator) ? path : path + Constants.DirectorySeperator;
                    Main.Context.API.ChangeQuery(string.IsNullOrEmpty(query.ActionKeyword) ?
                        changeTo :
                        query.ActionKeyword + " " + changeTo);
                    return false;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path, ShowIndexState = showIndexState, WindowsIndexed = windowsIndexed }
            };
        }

        internal static Result CreateOpenCurrentFolderResult(string path, bool windowsIndexed = false)
        {
            var retrievedDirectoryPath = FilesFolders.GetPreviousLevelDirectoryIfPathIncomplete(path);

            var folderName = retrievedDirectoryPath.TrimEnd(Constants.DirectorySeperator).Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None).Last();

            if (retrievedDirectoryPath.EndsWith(":\\"))
            {
                var driveLetter = path.Substring(0, 1).ToUpper();
                folderName = driveLetter + " drive";
            }

            var title = "Open current directory";

            if (retrievedDirectoryPath != path)
                title = "Open " + folderName;

            
            var subtitleFolderName = folderName;
            
            // ie. max characters can be displayed without subtitle cutting off: "Program Files (x86)"
            if (folderName.Length > 19)
                subtitleFolderName = "directory";

            return new Result
            {
                Title = title,
                SubTitle = $"Use > to search within {subtitleFolderName}, " +
                                $"* to search for file extensions or >* to combine both searches.",
                IcoPath = retrievedDirectoryPath,
                Score = 500,
                Action = c =>
                {
                    FilesFolders.OpenPath(retrievedDirectoryPath);
                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = retrievedDirectoryPath, ShowIndexState = true, WindowsIndexed = windowsIndexed }
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, bool showIndexState = false, bool windowsIndexed = false)
        {
            var result = new Result
            {
                Title = Path.GetFileName(filePath),
                SubTitle = filePath,
                IcoPath = filePath,
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, Path.GetFileName(filePath)).MatchData,
                Action = c =>
                {
                    try
                    {
                        FilesFolders.OpenPath(filePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Could not start " + filePath);
                    }

                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath, ShowIndexState = showIndexState, WindowsIndexed = windowsIndexed }
            };
            return result;
        }
    }

    internal class SearchResult
    {
        public string FullPath { get; set; }
        public ResultType Type { get; set; }

        public bool WindowsIndexed { get; set; }

        public bool ShowIndexState { get; set; }
    }

    internal enum ResultType
    {
        Volume,
        Folder,
        File
    }
}
