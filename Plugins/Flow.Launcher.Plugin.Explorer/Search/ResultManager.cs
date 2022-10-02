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

        internal static Result CreateDriveSpaceDisplayResult(string path, bool windowsIndexed = false)
        {
            var progressBarColor = "#26a0da";
            int progressValue = 0;
            var title = string.Empty; // hide title when use progress bar,
            var driveLetter = path.Substring(0, 1).ToUpper();
            var driveName = driveLetter + ":\\";
            DriveInfo drv = new DriveInfo(driveLetter);
            var subtitle = toReadableSize(drv.AvailableFreeSpace, 2) + " free of " + toReadableSize(drv.TotalSize, 2);
            double UsingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

            progressValue = Convert.ToInt32(UsingSize);

            if (progressValue >= 90)
                progressBarColor = "#da2626";

            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                AutoCompleteText = GetPathWithActionKeyword(path, ResultType.Folder),
                IcoPath = path,
                Score = 500,
                ProgressBar = progressValue,
                ProgressBarColor = progressBarColor,
                Action = c =>
                {
                    Context.API.OpenDirectory(path);
                    return true;
                },
                TitleToolTip = path,
                SubTitleToolTip = path,
                ContextData = new SearchResult
                {
                    Type = ResultType.Folder,
                    FullPath = path,
                    ShowIndexState = true,
                    WindowsIndexed = windowsIndexed
                }
            };
        }

        private static string toReadableSize(long pDrvSize, int pi)
        {
            int mok = 0;
            double drvSize = pDrvSize;
            string Space = "Byte";

            while (drvSize > 1024.0)
            {
                drvSize /= 1024.0;
                mok++;
            }

            if (mok == 1)
                Space = "KB";
            else if (mok == 2)
                Space = " MB";
            else if (mok == 3)
                Space = " GB";
            else if (mok == 4)
                Space = " TB";

            var returnStr = string.Format("{0}{1}", Convert.ToInt32(drvSize), Space);
            if (mok != 0)
            {
                switch (pi)
                {
                    case 1:
                        returnStr = string.Format("{0:F1}{1}", drvSize, Space);
                        break;
                    case 2:
                        returnStr = string.Format("{0:F2}{1}", drvSize, Space);
                        break;
                    case 3:
                        returnStr = string.Format("{0:F3}{1}", drvSize, Space);
                        break;
                    default:
                        returnStr = string.Format("{0}{1}", Convert.ToInt32(drvSize), Space);
                        break;
                }
            }

            return returnStr;
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

    internal class SearchResult
    {
        public string FullPath { get; set; }
        public ResultType Type { get; set; }

        public bool WindowsIndexed { get; set; }

        public bool ShowIndexState { get; set; }
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}
