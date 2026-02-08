#nullable enable
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Flow.Launcher.Plugin.Explorer.Search;

namespace Flow.Launcher.Plugin.Explorer.Views.Avalonia;

public partial class PreviewPanel : UserControl, INotifyPropertyChanged
{
    private static readonly string ClassName = nameof(PreviewPanel);

    public string FilePath { get; }
    public string FileName { get; }

    private string _fileSize = Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
    private string _createdAt = "";
    private string _lastModifiedAt = "";
    private IImage? _previewImage;

    public string FileSize
    {
        get => _fileSize;
        set
        {
            if (_fileSize != value)
            {
                _fileSize = value;
                OnPropertyChanged();
            }
        }
    }

    public string CreatedAt
    {
        get => _createdAt;
        set
        {
            if (_createdAt != value)
            {
                _createdAt = value;
                OnPropertyChanged();
            }
        }
    }

    public string LastModifiedAt
    {
        get => _lastModifiedAt;
        set
        {
            if (_lastModifiedAt != value)
            {
                _lastModifiedAt = value;
                OnPropertyChanged();
            }
        }
    }

    public IImage? PreviewImage
    {
        get => _previewImage;
        set
        {
            if (_previewImage != value)
            {
                _previewImage = value;
                OnPropertyChanged();
            }
        }
    }

    private Settings Settings { get; }

    public bool FileSizeVisibility => Settings.ShowFileSizeInPreviewPanel;
    public bool CreatedAtVisibility => Settings.ShowCreatedDateInPreviewPanel;
    public bool LastModifiedAtVisibility => Settings.ShowModifiedDateInPreviewPanel;

    public bool FileInfoVisibility =>
        Settings.ShowFileSizeInPreviewPanel ||
        Settings.ShowCreatedDateInPreviewPanel ||
        Settings.ShowModifiedDateInPreviewPanel;

    public PreviewPanel(Settings settings, string filePath, ResultType type)
    {
        Settings = settings;
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);

        InitializeComponent();
        DataContext = this;

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
                    var size = GetFolderSize(filePath);
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => FileSize = size);
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
        try
        {
            var imagePath = FilePath;
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return;

            var bitmap = new Bitmap(imagePath);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PreviewImage = bitmap);
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to load image for {FilePath}", e);
        }
    }

    private void InitializeComponent()
    {
        global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
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
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to file: {filePath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file size for {filePath}", e);
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
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
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to file: {filePath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file created date for {filePath}", e);
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
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
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to file: {filePath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file modified date for {filePath}", e);
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
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
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (OperationCanceledException)
        {
            Main.Context.API.LogError(ClassName, $"Operation timed out while calculating folder size for {folderPath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        // For parallel operations, AggregateException may be thrown if any of the tasks fail
        catch (AggregateException ae)
        {
            switch (ae.InnerException)
            {
                case FileNotFoundException:
                    Main.Context.API.LogError(ClassName, $"Folder not found: {folderPath}");
                    return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
                case UnauthorizedAccessException:
                    Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
                    return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
                case OperationCanceledException:
                    Main.Context.API.LogError(ClassName, $"Operation timed out while calculating folder size for {folderPath}");
                    return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
                default:
                    Main.Context.API.LogException(ClassName, $"Failed to get folder size for {folderPath}", ae);
                    return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
            }
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get folder size for {folderPath}", e);
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
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
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get folder created date for {folderPath}", e);
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
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
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.API.LogError(ClassName, $"Access denied to folder: {folderPath}");
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get folder modified date for {folderPath}", e);
            return Localize.plugin_explorer_plugin_tooltip_more_info_unknown();
        }
    }

    private static string GetFileAge(DateTime fileDateTime)
    {
        var now = DateTime.Now;
        var difference = now - fileDateTime;

        if (difference.TotalDays < 1)
            return Localize.Today();
        if (difference.TotalDays < 30)
            return Localize.DaysAgo((int)difference.TotalDays);

        var monthsDiff = (now.Year - fileDateTime.Year) * 12 + now.Month - fileDateTime.Month;
        if (monthsDiff == 1)
            return Localize.OneMonthAgo();
        if (monthsDiff < 12)
            return Localize.MonthsAgo(monthsDiff);

        var yearsDiff = now.Year - fileDateTime.Year;
        if (now.Month < fileDateTime.Month || (now.Month == fileDateTime.Month && now.Day < fileDateTime.Day))
            yearsDiff--;

        return yearsDiff == 1 ? Localize.OneYearAgo() : Localize.YearsAgo(yearsDiff);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
