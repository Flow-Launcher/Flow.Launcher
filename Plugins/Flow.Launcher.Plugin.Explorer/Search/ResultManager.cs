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

        internal static Result CreateOpenCurrentFolderResult(string path, bool isPreviousDirectoryLevel)
        {
            var folderName = path;

            if (folderName.EndsWith(":\\"))
            {
                var driveLetter = folderName.Substring(0, 1).ToUpper();
                folderName = driveLetter + " drive";
            }
            else
            {
                folderName = folderName.TrimEnd(Constants.DirectorySeperator).Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None).Last();
            }

            var firstResult = "";

            if (isPreviousDirectoryLevel)
            {
                firstResult = "Open " + folderName;
            }
            else
            {
                firstResult = "Open current directory";
            }

            return new Result
            {
                Title = firstResult,
                SubTitle = $"Use > to search files and subfolders within {folderName}, " +
                                $"* to search for file extensions in {folderName} or both >* to combine the search",
                IcoPath = path,
                Score = 500,
                Action = c =>
                {
                    FilesFolders.OpenPath(path);
                    return true;
                }
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
