using Flow.Launcher.Core.Resource;
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
                    result.FullPath, result.FullPath, query, 0, result.WindowsIndexed),
                ResultType.File => CreateFileResult(
                    result.FullPath, query, 0, result.WindowsIndexed),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal static Result CreateFolderResult(string title, string subtitle, string path, Query query, int score = 0, bool windowsIndexed = false)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = Path.GetDirectoryName(path),
                AutoCompleteText = GetPathWithActionKeyword(path, ResultType.Folder),
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                CopyText = path,
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
                TitleToolTip = InternationalizationManager.Instance.GetTranslation("plugin_explorer_plugin_ToolTipOpenDirectory"),
                SubTitleToolTip = path,
                ContextData = new SearchResult
                {
                    Type = ResultType.Folder,
                    FullPath = path,
                    WindowsIndexed = windowsIndexed
                }
            };
        }

        internal static Result CreateDriveSpaceDisplayResult(string path, bool windowsIndexed = false)
        {
            var progressBarColor = "#26a0da";
            var title = string.Empty; // hide title when use progress bar,
            var driveLetter = path[..1].ToUpper();
            var driveName = driveLetter + ":\\";
            DriveInfo drv = new DriveInfo(driveLetter);
            var subtitle = ToReadableSize(drv.AvailableFreeSpace, 2) + " free of " + ToReadableSize(drv.TotalSize, 2);
            double usingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

            int? progressValue = Convert.ToInt32(usingSize);

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
                    Type = ResultType.Volume,
                    FullPath = path,
                    WindowsIndexed = windowsIndexed
                }
            };
        }

        private static string ToReadableSize(long pDrvSize, int pi)
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

            var returnStr = $"{Convert.ToInt32(drvSize)}{Space}";
            if (mok != 0)
            {
                returnStr = pi switch
                {
                    1 => $"{drvSize:F1}{Space}",
                    2 => $"{drvSize:F2}{Space}",
                    3 => $"{drvSize:F3}{Space}",
                    _ => $"{Convert.ToInt32(drvSize)}{Space}"
                };
            }

            return returnStr;
        }

        internal static Result CreateOpenCurrentFolderResult(string path, bool windowsIndexed = false)
        {
            var folderName = path.TrimEnd(Constants.DirectorySeperator).Split(new[]
            {
                Path.DirectorySeparatorChar
            }, StringSplitOptions.None).Last();

            var title = $"Open {folderName}";

            var subtitleFolderName = folderName;

            // ie. max characters can be displayed without subtitle cutting off: "Program Files (x86)"
            if (folderName.Length > 19)
                subtitleFolderName = "the directory";

            return new Result
            {
                Title = title,
                SubTitle = $"Use > to search within {subtitleFolderName}, " +
                           $"* to search for file extensions or >* to combine both searches.",
                AutoCompleteText = GetPathWithActionKeyword(path, ResultType.Folder),
                IcoPath = path,
                Score = 500,
                CopyText = path,
                Action = _ =>
                {
                    Context.API.OpenDirectory(path);
                    return true;
                },
                ContextData = new SearchResult
                {
                    Type = ResultType.Folder,
                    FullPath = path,
                    WindowsIndexed = windowsIndexed
                }
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, int score = 0, bool windowsIndexed = false)
        {
            Result.PreviewInfo preview = IsMedia(Path.GetExtension(filePath)) ? new Result.PreviewInfo {
                IsMedia = true,
                PreviewImagePath = filePath,
            } : Result.PreviewInfo.Default;

            var result = new Result
            {
                Title = Path.GetFileName(filePath),
                SubTitle = Path.GetDirectoryName(filePath),
                IcoPath = filePath,
                Preview = preview,
                AutoCompleteText = GetPathWithActionKeyword(filePath, ResultType.File),
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, Path.GetFileName(filePath)).MatchData,
                Score = score,
                CopyText = filePath,
                Action = c =>
                {
                    try
                    {
                        if (File.Exists(filePath) && c.SpecialKeyState.CtrlPressed && c.SpecialKeyState.ShiftPressed)
                        {
                            _ = Task.Run(() =>
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
                TitleToolTip = InternationalizationManager.Instance.GetTranslation("plugin_explorer_plugin_ToolTipOpenContainingFolder"),
                SubTitleToolTip = filePath,
                ContextData = new SearchResult
                {
                    Type = ResultType.File,
                    FullPath = filePath,
                    WindowsIndexed = windowsIndexed
                }
            };
            return result;
        }

        public static bool IsMedia(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            { 
                return false; 
            }
            else
            {
                return MediaExtensions.Contains(extension.ToLowerInvariant());
            }
        }

        public static readonly string[] MediaExtensions = { ".jpg", ".png", ".avi", ".mkv", ".bmp", ".gif", ".wmv", ".mp3", ".flac", ".mp4" };
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}
