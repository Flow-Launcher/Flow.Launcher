using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Views;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using Peter;
using Path = System.IO.Path;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class ResultManager
    {
        private static readonly string ClassName = nameof(ResultManager);

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
                _ => throw new ArgumentOutOfRangeException(null)
            };
        }

        internal static void ShowNativeContextMenu(string path, ResultType type)
        {
            var screenWithMouseCursor = MonitorInfo.GetCursorDisplayMonitor();
            var xOfScreenCenter = screenWithMouseCursor.WorkingArea.Left + screenWithMouseCursor.WorkingArea.Width / 2;
            var yOfScreenCenter = screenWithMouseCursor.WorkingArea.Top + screenWithMouseCursor.WorkingArea.Height / 2;
            var showPosition = new System.Drawing.Point((int)xOfScreenCenter, (int)yOfScreenCenter);

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
                TitleHighlightData = Context.API.FuzzySearch(query.Search, title).MatchData,
                CopyText = path,
                Preview = new Result.PreviewInfo
                {
                    FilePath = path,
                },
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Settings, path, ResultType.Folder)),
                Action = c =>
                {
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
                            Context.API.ShowMsgBox(ex.Message, Localize.plugin_explorer_opendir_error());
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
                TitleToolTip = Localize.plugin_explorer_plugin_ToolTipOpenDirectory(),
                SubTitleToolTip = Settings.DisplayMoreInformationInToolTip ? GetFolderMoreInfoTooltip(path) : path,
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path, WindowsIndexed = windowsIndexed },
                HotkeyIds = new List<int>
                {
                    0, 1, 2, 3
                },
            };
        }

        internal static Result CreateDriveSpaceDisplayResult(string path, string actionKeyword, int score)
        {
            return CreateDriveSpaceDisplayResult(path, actionKeyword, score, SearchManager.UseIndexSearch(path));
        }

        internal static Result CreateDriveSpaceDisplayResult(string path, string actionKeyword, bool windowsIndexed = false)
        {
            return CreateDriveSpaceDisplayResult(path, actionKeyword, 500, windowsIndexed);
        }

        private static Result CreateDriveSpaceDisplayResult(string path, string actionKeyword, int score, bool windowsIndexed = false)
        {
            var progressBarColor = "#26a0da";
            var title = string.Empty; // hide title when use progress bar,
            var driveLetter = path[..1].ToUpper();
            DriveInfo drv = new DriveInfo(driveLetter);
            var freespace = ToReadableSize(drv.AvailableFreeSpace, 2);
            var totalspace = ToReadableSize(drv.TotalSize, 2);
            var subtitle = Localize.plugin_explorer_diskfreespace(freespace, totalspace);
            double usingSize = (Convert.ToDouble(drv.TotalSize) - Convert.ToDouble(drv.AvailableFreeSpace)) / Convert.ToDouble(drv.TotalSize) * 100;

            int? progressValue = Convert.ToInt32(usingSize);

            if (progressValue >= 90)
                progressBarColor = "#da2626";

            var tooltip = Settings.DisplayMoreInformationInToolTip
                ? GetVolumeMoreInfoTooltip(path, freespace, totalspace)
                : path;

            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                AutoCompleteText = GetPathWithActionKeyword(path, ResultType.Folder, actionKeyword),
                IcoPath = path,
                Score = score,
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
                TitleToolTip = tooltip,
                SubTitleToolTip = tooltip,
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
                Title = Localize.plugin_explorer_openresultfolder(),
                SubTitle = Localize.plugin_explorer_openresultfolder_subtitle(),
                AutoCompleteText = GetPathWithActionKeyword(folderPath, ResultType.Folder, actionKeyword),
                IcoPath = folderPath,
                Score = 500,
                CopyText = folderPath,
                Action = c =>
                {
                    OpenFolder(folderPath);
                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = folderPath, WindowsIndexed = windowsIndexed },
                HotkeyIds = new List<int>
                {
                    1
                },
            };
        }

        internal static Result CreateFileResult(string filePath, Query query, int score = 0, bool windowsIndexed = false)
        {
            var isMedia = IsMedia(Path.GetExtension(filePath));
            var title = Path.GetFileName(filePath) ?? string.Empty;
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            /* Preview Detail */

            var result = new Result
            {
                Title = title,
                SubTitle = directory,
                IcoPath = filePath,
                Preview = new Result.PreviewInfo
                {
                    IsMedia = isMedia,
                    PreviewImagePath = isMedia ? filePath : null,
                    FilePath = filePath,
                },
                AutoCompleteText = GetAutoCompleteText(title, query, filePath, ResultType.File),
                TitleHighlightData = Context.API.FuzzySearch(query.Search, title).MatchData,
                Score = score,
                CopyText = filePath,
                PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(Settings, filePath, ResultType.File)),
                Action = c =>
                {
                    try
                    {
                        OpenFile(filePath, Settings.UseLocationAsWorkingDir ? directory : string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Context.API.ShowMsgBox(ex.Message, Localize.plugin_explorer_openfile_error());
                    }

                    return true;
                },
                TitleToolTip = Localize.plugin_explorer_plugin_ToolTipOpenContainingFolder(),
                SubTitleToolTip = Settings.DisplayMoreInformationInToolTip ? GetFileMoreInfoTooltip(filePath) : filePath,
                ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath, WindowsIndexed = windowsIndexed },
                HotkeyIds = new List<int>
                {
                    0, 1, 2, 3
                },
            };
            return result;
        }

        private static bool IsMedia(string extension)
        {
            if (string.IsNullOrEmpty(extension)) { return false; }

            return MediaExtensions.Contains(extension.ToLowerInvariant());
        }

        public static void OpenFile(string filePath, string workingDir = "", bool asAdmin = false)
        {
            IncrementEverythingRunCounterIfNeeded(filePath);
            FilesFolders.OpenFile(filePath, workingDir, asAdmin, (string str) => Context.API.ShowMsgBox(str));
        }

        public static void OpenFolder(string folderPath, string fileNameOrFilePath = null)
        {
            IncrementEverythingRunCounterIfNeeded(folderPath);
            Context.API.OpenDirectory(folderPath, fileNameOrFilePath);
        }

        private static void IncrementEverythingRunCounterIfNeeded(string fileOrFolder)
        {
            if (Settings.EverythingEnabled && Settings.EverythingEnableRunCount)
                _ = Task.Run(() => EverythingApi.IncrementRunCounterAsync(fileOrFolder));
        }

        private static string GetFileMoreInfoTooltip(string filePath)
        {
            try
            {
                var fileSize = PreviewPanel.GetFileSize(filePath);
                var fileCreatedAt = PreviewPanel.GetFileCreatedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                var fileModifiedAt = PreviewPanel.GetFileLastModifiedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                return Localize.plugin_explorer_plugin_tooltip_more_info(filePath, fileSize, fileCreatedAt, fileModifiedAt, Environment.NewLine);
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to load tooltip for {filePath}", e);
                return filePath;
            }
        }

        private static string GetFolderMoreInfoTooltip(string folderPath)
        {
            try
            {
                var folderSize = PreviewPanel.GetFolderSize(folderPath);
                var folderCreatedAt = PreviewPanel.GetFolderCreatedAt(folderPath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                var folderModifiedAt = PreviewPanel.GetFolderLastModifiedAt(folderPath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
                return Localize.plugin_explorer_plugin_tooltip_more_info(folderPath, folderSize, folderCreatedAt, folderModifiedAt, Environment.NewLine);
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to load tooltip for {folderPath}", e);
                return folderPath;
            }
        }

        private static string GetVolumeMoreInfoTooltip(string volumePath, string freespace, string totalspace)
        {
            return Localize.plugin_explorer_plugin_tooltip_more_info_volume(volumePath, freespace, totalspace, Environment.NewLine);
        }

        private static readonly string[] MediaExtensions = 
        { 
            ".jpg", ".png", ".avi", ".mkv", ".bmp", ".gif", ".wmv", ".mp3", ".flac", ".mp4",
            ".m4a", ".m4v", ".heic", ".mov", ".flv", ".webm"
        };
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}
