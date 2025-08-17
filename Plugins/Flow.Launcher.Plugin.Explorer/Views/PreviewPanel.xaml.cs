using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Plugin.Explorer.Search;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.Explorer.Views;

#nullable enable

[INotifyPropertyChanged]
public partial class PreviewPanel : UserControl
{
    private static readonly string ClassName = nameof(PreviewPanel);

    public string FilePath { get; }
    public string FileName { get; }

    [ObservableProperty]
    private string _fileSize = Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
    
    [ObservableProperty]
    private string _createdAt = "";
    
    [ObservableProperty]
    private string _lastModifiedAt = "";

    [ObservableProperty]
    private ImageSource _previewImage = new BitmapImage();

    private Settings Settings { get; }

    public Visibility FileSizeVisibility => Settings.ShowFileSizeInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility CreatedAtVisibility => Settings.ShowCreatedDateInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility LastModifiedAtVisibility => Settings.ShowModifiedDateInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility FileInfoVisibility =>
        Settings.ShowFileSizeInPreviewPanel ||
        Settings.ShowCreatedDateInPreviewPanel ||
        Settings.ShowModifiedDateInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;

    public PreviewPanel(Settings settings, string filePath, ResultType type)
    {
        Settings = settings;
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);

        InitializeComponent();

        if (Settings.ShowFileSizeInPreviewPanel)
        {
            if (type == ResultType.File)
            {
                FileSize = GetFileSize(filePath);
            }
            else
            {
                _ = Task.Run(() =>
                {
                    FileSize = GetFolderSize(filePath);
                    OnPropertyChanged(nameof(FileSize));
                }).ConfigureAwait(false);
            }
        }

        if (Settings.ShowCreatedDateInPreviewPanel)
        {
            CreatedAt = type == ResultType.File ?
                GetFileCreatedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel) :
                GetFolderCreatedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
        }

        if (Settings.ShowModifiedDateInPreviewPanel)
        {
            LastModifiedAt = type == ResultType.File ? 
                GetFileLastModifiedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel) :
                GetFolderLastModifiedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
        }

        _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        PreviewImage = await Main.Context.API.LoadImageAsync(FilePath, true).ConfigureAwait(false);
    }

    public static string GetFileSize(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return ResultManager.ToReadableSize(fileInfo.Length, 2);
        }
        catch (FileNotFoundException)
        {
            Main.Context.API.LogError(ClassName, $"File not found: {filePath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to file: {filePath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file size for {filePath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    public static string GetFileCreatedAt(string filePath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var createdDate = File.GetCreationTime(filePath);
            var formattedDate = createdDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(createdDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.API.LogError(ClassName, $"File not found: {filePath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to file: {filePath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file created date for {filePath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    public static string GetFileLastModifiedAt(string filePath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var lastModifiedDate = File.GetLastWriteTime(filePath);
            var formattedDate = lastModifiedDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(lastModifiedDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.API.LogError(ClassName, $"File not found: {filePath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to file: {filePath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file modified date for {filePath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    public static string GetFolderSize(string folderPath)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            // Use parallel enumeration for better performance
            var directoryInfo = new DirectoryInfo(folderPath);
            long size = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .AsParallel()
                .WithCancellation(timeoutCts.Token)
                .Sum(file => file.Length);

            return ResultManager.ToReadableSize(size, 2);
        }
        catch (FileNotFoundException)
        {
            Main.Context.API.LogError(ClassName, $"Folder not found: {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (OperationCanceledException)
        {
            Main.Context.API.LogError(ClassName, $"Operation timed out while calculating folder size for {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        // For parallel operations, AggregateException may be thrown if any of the tasks fail
        catch (AggregateException ae)
        {
            switch (ae.InnerException)
            {
                case FileNotFoundException:
                    Main.Context.API.LogError(ClassName, $"Folder not found: {folderPath}");
                    return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
                case UnauthorizedAccessException:
                    Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
                    return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
                case OperationCanceledException:
                    Main.Context.API.LogError(ClassName, $"Operation timed out while calculating folder size for {folderPath}");
                    return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
                default:
                    Main.Context.API.LogException(ClassName, $"Failed to get folder size for {folderPath}", ae);
                    return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
            }
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get folder size for {folderPath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    public static string GetFolderCreatedAt(string folderPath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var createdDate = Directory.GetCreationTime(folderPath);
            var formattedDate = createdDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(createdDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.API.LogError(ClassName, $"Folder not found: {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get folder created date for {folderPath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    public static string GetFolderLastModifiedAt(string folderPath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var lastModifiedDate = Directory.GetLastWriteTime(folderPath);
            var formattedDate = lastModifiedDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(lastModifiedDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.API.LogError(ClassName, $"Folder not found: {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get folder modified date for {folderPath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    private static string GetFileAge(DateTime fileDateTime)
    {
        var now = DateTime.Now;
        var difference = now - fileDateTime;

        if (difference.TotalDays < 1)
            return Main.Context.API.GetTranslation("Today");
        if (difference.TotalDays < 30)
            return string.Format(Main.Context.API.GetTranslation("DaysAgo"), (int)difference.TotalDays);

        var monthsDiff = (now.Year - fileDateTime.Year) * 12 + now.Month - fileDateTime.Month;
        if (monthsDiff == 1)
            return Main.Context.API.GetTranslation("OneMonthAgo");
        if (monthsDiff < 12)
            return string.Format(Main.Context.API.GetTranslation("MonthsAgo"), monthsDiff);

        var yearsDiff = now.Year - fileDateTime.Year;
        if (now.Month < fileDateTime.Month || (now.Month == fileDateTime.Month && now.Day < fileDateTime.Day))
            yearsDiff--; 

        return yearsDiff == 1 ? Main.Context.API.GetTranslation("OneYearAgo") :
            string.Format(Main.Context.API.GetTranslation("YearsAgo"), yearsDiff);
    }
}
