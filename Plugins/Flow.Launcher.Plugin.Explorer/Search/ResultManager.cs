using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class ResultManager
    {
        private static PluginInitContext Context;
        private static Settings Settings { get; set; }

        public static void Init(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }

        private static string GetPathWithActionKeyword(string path, ResultType type)
        {
            // one of it is enabled
            var keyword = Settings.SearchActionKeywordEnabled ? Settings.SearchActionKeyword : Settings.PathSearchActionKeyword;

            keyword = keyword == Query.GlobalPluginWildcardSign ? string.Empty : keyword + " ";

            var formatted_path = path;

            if (type == ResultType.Folder)
                formatted_path = path.EndsWith(Constants.DirectorySeperator) ? path : path + Constants.DirectorySeperator;

            return $"{keyword}{formatted_path}";
        }

        public static Result CreateResult(Query query, SearchResult result)
        {
            return result.Type switch
            {
                ResultType.Folder or ResultType.Volume => CreateFolderResult(Path.GetFileName(result.FullPath),
                    result.FullPath, result.FullPath, query, 0, result.ShowIndexState, result.WindowsIndexed),
                ResultType.File => CreateFileResult(
                    result.FullPath, query, 0, result.ShowIndexState, result.WindowsIndexed),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal static Result CreateFolderResult(string title, string subtitle, string path, Query query, int score = 0, bool showIndexState = false, bool windowsIndexed = false)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = subtitle,
                AutoCompleteText = GetPathWithActionKeyword(path, ResultType.Folder),
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                Action = c =>
                {
                    if (c.SpecialKeyState.CtrlPressed || (!Settings.PathSearchKeywordEnabled && !Settings.SearchActionKeywordEnabled))
                    {
                        try
                        {
                            Context.API.OpenDirectory(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Could not start " + path);
                            return false;
                        }
                    }

                    Context.API.ChangeQuery(GetPathWithActionKeyword(path, ResultType.Folder));

                    return false;
                },
                Score = score,
                TitleToolTip = Constants.ToolTipOpenDirectory,
                SubTitleToolTip = path,
                ContextData = new SearchResult
                {
                    Type = ResultType.Folder,
                    FullPath = path,
                    ShowIndexState = showIndexState,
                    WindowsIndexed = windowsIndexed
                }
            };
        }

        internal static Result CreateOpenCurrentFolderResult(string path, bool windowsIndexed = false)
        {
            var retrievedDirectoryPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path);

            var folderName = retrievedDirectoryPath.TrimEnd(Constants.DirectorySeperator).Split(new[]
            {
                Path.DirectorySeparatorChar
            }, StringSplitOptions.None).Last();

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
                subtitleFolderName = "the directory";

            return new Result
            {
                Title = title,
                SubTitle = $"Use > to search within {subtitleFolderName}, " +
                           $"* to search for file extensions or >* to combine both searches.",
                AutoCompleteText = GetPathWithActionKeyword(retrievedDirectoryPath, ResultType.Folder),
                IcoPath = retrievedDirectoryPath,
                Score = 500,
                Action = c =>
                {
                    Context.API.OpenDirectory(retrievedDirectoryPath);
                    return true;
                },
                TitleToolTip = retrievedDirectoryPath,
                SubTitleToolTip = retrievedDirectoryPath,
                ContextData = new SearchResult
                {
                    Type = ResultType.Folder,
                    FullPath = retrievedDirectoryPath,
                    ShowIndexState = true,
                    WindowsIndexed = windowsIndexed
                }
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, int score = 0, bool showIndexState = false, bool windowsIndexed = false)
        {
            var result = new Result
            {
                Title = Path.GetFileName(filePath),
                SubTitle = filePath,
                IcoPath = filePath,
                AutoCompleteText = GetPathWithActionKeyword(filePath, ResultType.File),
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, Path.GetFileName(filePath)).MatchData,
                Score = score,
                Action = c =>
                {
                    try
                    {
                        if (File.Exists(filePath) && c.SpecialKeyState.CtrlPressed && c.SpecialKeyState.ShiftPressed)
                        {
                            Task.Run(() =>
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = filePath,
                                        UseShellExecute = true,
                                        Verb = "runas",
                                    });
                                }
                                catch (Exception e)
                                {
                                    MessageBox.Show(e.Message, "Could not start " + filePath);
                                }
                            });
                        }
                        else if (c.SpecialKeyState.CtrlPressed)
                        {
                            Context.API.OpenDirectory(Path.GetDirectoryName(filePath), filePath);
                        }
                        else
                        {
                            FilesFolders.OpenPath(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Could not start " + filePath);
                    }

                    return true;
                },
                TitleToolTip = Constants.ToolTipOpenContainingFolder,
                SubTitleToolTip = filePath,
                ContextData = new SearchResult
                {
                    Type = ResultType.File,
                    FullPath = filePath,
                    ShowIndexState = showIndexState,
                    WindowsIndexed = windowsIndexed
                }
            };
            return result;
        }
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}