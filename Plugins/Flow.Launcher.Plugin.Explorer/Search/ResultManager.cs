using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using System.Windows.Input;
using Path = System.IO.Path;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.Views;
using Peter;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class ResultManager
    {
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };
        private static PluginInitContext Context;
        private static Settings Settings { get; set; }

        public static void Init(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }

        public static string GetPathWithActionKeyword(string path, ResultType type, string actionKeyword)
        {
            // actionKeyword will be empty string if using global, query.ActionKeyword is ""

            var usePathSearchActionKeyword = Settings.PathSearchKeywordEnabled && !Settings.SearchActionKeywordEnabled;

            var pathSearchActionKeyword = Settings.PathSearchActionKeyword == Query.GlobalPluginWildcardSign
                ? string.Empty
                : $"{Settings.PathSearchActionKeyword} ";

            var searchActionKeyword = Settings.SearchActionKeyword == Query.GlobalPluginWildcardSign
                ? string.Empty
                : $"{Settings.SearchActionKeyword} ";

            var keyword = usePathSearchActionKeyword ? pathSearchActionKeyword : searchActionKeyword;

            var formattedPath = path;

            if (type == ResultType.Folder)
                // the separator is needed so when navigating the folder structure contents of the folder are listed
                formattedPath = path.EndsWith(Constants.DirectorySeparator) ? path : path + Constants.DirectorySeparator;

            return $"{keyword}{formattedPath}";
        }

        public static string GetAutoCompleteText(string title, Query query, string path, ResultType resultType)
        {
            return !Settings.PathSearchKeywordEnabled && !Settings.SearchActionKeywordEnabled
                ? $"{query.ActionKeyword} {title}" // Only Quick Access action keyword is used in this scenario
                : GetPathWithActionKeyword(path, resultType, query.ActionKeyword);
        }

        public static Result CreateResult(Query query, SearchResult result)
        {
            return result.Type switch
            {
                ResultType.Folder or ResultType.Volume =>
                    CreateFolderResult(Path.GetFileName(result.FullPath), result.FullPath, result.FullPath, query, result.Score, result.WindowsIndexed),
                ResultType.File =>
                    CreateFileResult(result.FullPath, query, result.Score, result.WindowsIndexed),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal static void ShowNativeContextMenu(string path, ResultType type)
        {
            var screenWithMouseCursor = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var xOfScreenCenter = screenWithMouseCursor.WorkingArea.Left + screenWithMouseCursor.WorkingArea.Width / 2;
            var yOfScreenCenter = screenWithMouseCursor.WorkingArea.Top + screenWithMouseCursor.WorkingArea.Height / 2;
            var showPosition = new System.Drawing.Point(xOfScreenCenter, yOfScreenCenter);

            switch (type)
            {
                case ResultType.File:
                    var fileInfo = new FileInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(fileInfo, showPosition);
                    break;

                case ResultType.Folder:
                    var folderInfo = new System.IO.DirectoryInfo[] { new(path) };
                    new ShellContextMenu().ShowContextMenu(folderInfo, showPosition);
                    break;
            }
        }

        internal static Result CreateFolderResult(string title, string subtitle, string path, Query query, int score = 0, bool windowsIndexed = false)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = subtitle,
                AutoCompleteText = GetAutoCompleteText(title, query, path, ResultType.Folder),
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                CopyText = path,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                Action = c =>
                {
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Alt)
                    {
                        ShowNativeContextMenu(path, ResultType.Folder);
                        return false;
                    }
                    // open folder
                    if (c.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        try
                        {
                            OpenFolder(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Context.API.ShowMsgBox(ex.Message, Context.API.GetTranslation("plugin_explorer_opendir_error"));
                            return false;
                        }
                    }
                    // Open containing folder
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control)
                    {
                        try
                        {
                            Context.API.OpenDirectory(Path.GetDirectoryName(path), path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Context.API.ShowMsgBox(ex.Message, Context.API.GetTranslation("plugin_explorer_opendir_error"));
                            return false;
                        }
                    }

                    // If path search is disabled just open it in file manager
                    if (Settings.DefaultOpenFolderInFileManager || (!Settings.PathSearchKeywordEnabled && !Settings.SearchActionKeywordEnabled))
                    {
                        try
                        {
                            OpenFolder(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Context.API.ShowMsgBox(ex.Message, Context.API.GetTranslation("plugin_explorer_opendir_error"));
                            return false;
                        }
                    }
                    else
                    {
                        // or make this folder the current query
                        Context.API.ChangeQuery(GetPathWithActionKeyword(path, ResultType.Folder, query.ActionKeyword));
                    }

                    return false;
                },
                Score = score,
                TitleToolTip = InternationalizationManager.Instance.GetTranslation("plugin_explorer_plugin_ToolTipOpenDirectory"),
                SubTitleToolTip = path,
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path, WindowsIndexed = windowsIndexed }
            };
        }

        internal static Result CreateDriveSpaceDisplayResult(string path, string actionKeyword, bool windowsIndexed = false)
        {
            var progressBarColor = "#26a0da";
            var title = string.Empty; // hide title when use progress bar,
            var driveLetter = path[..1].ToUpper();
            DriveInfo drv = new DriveInfo(driveLetter);
            var freespace = ToReadableSize(drv.AvailableFreeSpace, 2);
            var totalspace = ToReadableSize(drv.TotalSize, 2);
            var subtitle = string.Format(Context.API.GetTranslation("plugin_explorer_diskfreespace"), freespace, totalspace);
            double usingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

            int? progressValue = Convert.ToInt32(usingSize);

            if (progressValue >= 90)
                progressBarColor = "#da2626";

            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                AutoCompleteText = GetPathWithActionKeyword(path, ResultType.Folder, actionKeyword),
                IcoPath = path,
                Score = 500,
                ProgressBar = progressValue,
                ProgressBarColor = progressBarColor,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                Action = _ =>
                {
                    OpenFolder(path);
                    return true;
                },
                TitleToolTip = path,
                SubTitleToolTip = path,
                ContextData = new SearchResult { Type = ResultType.Volume, FullPath = path, WindowsIndexed = windowsIndexed }
            };
        }

        internal static string ToReadableSize(long sizeOnDrive, int pi)
        {
            var unitIndex = 0;
            double readableSize = sizeOnDrive;

            while (readableSize > 1024.0 && unitIndex < SizeUnits.Length - 1)
            {
                readableSize /= 1024.0;
                unitIndex++;
            }

            var unit = SizeUnits[unitIndex] ?? "";

            var returnStr = $"{Convert.ToInt32(readableSize)} {unit}";
            if (unitIndex != 0)
            {
                returnStr = pi switch
                {
                    1 => $"{readableSize:F1} {unit}",
                    2 => $"{readableSize:F2} {unit}",
                    3 => $"{readableSize:F3} {unit}",
                    _ => $"{Convert.ToInt32(readableSize)} {unit}"
                };
            }

            return returnStr;
        }

        internal static Result CreateOpenCurrentFolderResult(string path, string actionKeyword, bool windowsIndexed = false)
        {
            // Path passed from PathSearchAsync ends with Constants.DirectorySeparator ('\'), need to remove the separator
            // so it's consistent with folder results returned by index search which does not end with one
            var folderPath = path.TrimEnd(Constants.DirectorySeparator);

            return new Result
            {
                Title = Context.API.GetTranslation("plugin_explorer_openresultfolder"),
                SubTitle = Context.API.GetTranslation("plugin_explorer_openresultfolder_subtitle"),
                AutoCompleteText = GetPathWithActionKeyword(folderPath, ResultType.Folder, actionKeyword),
                IcoPath = folderPath,
                Score = 500,
                CopyText = folderPath,
                Action = c =>
                {
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Alt)
                    {
                        ShowNativeContextMenu(folderPath, ResultType.Folder);
                        return false;
                    }
                    OpenFolder(folderPath);
                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = folderPath, WindowsIndexed = windowsIndexed }
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, int score = 0, bool windowsIndexed = false)
        {
            bool isMedia = IsMedia(Path.GetExtension(filePath));
            var title = Path.GetFileName(filePath);


            /* Preview Detail */

            var result = new Result
            {
                Title = title,
                SubTitle = Path.GetDirectoryName(filePath),
                IcoPath = filePath,
                Preview = new Result.PreviewInfo
                {
                    IsMedia = isMedia,
                    PreviewImagePath = isMedia ? filePath : null,
                    FilePath = filePath,
                },
                AutoCompleteText = GetAutoCompleteText(title, query, filePath, ResultType.File),
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                Score = score,
                CopyText = filePath,
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Settings, filePath)),
                Action = c =>
                {
                    if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Alt)
                    {
                        ShowNativeContextMenu(filePath, ResultType.File);
                        return false;
                    }
                    try
                    {
                        if (c.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift))
                        {
                            OpenFile(filePath, Settings.UseLocationAsWorkingDir ? Path.GetDirectoryName(filePath) : string.Empty, true);
                        }
                        else if (c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control)
                        {
                            OpenFolder(filePath, filePath);
                        }
                        else
                        {
                            OpenFile(filePath, Settings.UseLocationAsWorkingDir ? Path.GetDirectoryName(filePath) : string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsgBox(ex.Message, Context.API.GetTranslation("plugin_explorer_openfile_error"));
                    }

                    return true;
                },
                TitleToolTip = InternationalizationManager.Instance.GetTranslation("plugin_explorer_plugin_ToolTipOpenContainingFolder"),
                SubTitleToolTip = filePath,
                ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath, WindowsIndexed = windowsIndexed }
            };
            return result;
        }

        private static bool IsMedia(string extension)
        {
            if (string.IsNullOrEmpty(extension)) { return false; }

            return MediaExtensions.Contains(extension.ToLowerInvariant());
        }

        private static void OpenFile(string filePath, string workingDir = "", bool asAdmin = false)
        {
            IncrementEverythingRunCounterIfNeeded(filePath);
            FilesFolders.OpenFile(filePath, workingDir, asAdmin, (string str) => Context.API.ShowMsgBox(str));
        }

        private static void OpenFolder(string folderPath, string fileNameOrFilePath = null)
        {
            IncrementEverythingRunCounterIfNeeded(folderPath);
            Context.API.OpenDirectory(folderPath, fileNameOrFilePath);
        }

        private static void IncrementEverythingRunCounterIfNeeded(string fileOrFolder)
        {
            if (Settings.EverythingEnabled && Settings.EverythingEnableRunCount)
                _ = Task.Run(() => EverythingApi.IncrementRunCounterAsync(fileOrFolder));
        }

        private static readonly string[] MediaExtensions = { ".jpg", ".png", ".avi", ".mkv", ".bmp", ".gif", ".wmv", ".mp3", ".flac", ".mp4" };
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}
