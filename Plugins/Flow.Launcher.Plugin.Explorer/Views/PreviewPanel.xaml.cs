﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Plugin.Explorer.Search;

namespace Flow.Launcher.Plugin.Explorer.Views;

#nullable enable

public partial class PreviewPanel : UserControl, INotifyPropertyChanged
{
    private static readonly string ClassName = nameof(PreviewPanel);

    private string FilePath { get; }
    public string FileSize { get; } = "";
    public string CreatedAt { get; } = "";
    public string LastModifiedAt { get; } = "";
    private ImageSource _previewImage = new BitmapImage();
    private Settings Settings { get; }

    public ImageSource PreviewImage
    {
        get => _previewImage;
        private set
        {
            _previewImage = value;
            OnPropertyChanged();
        }
    }

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
        InitializeComponent();

        Settings = settings;

        FilePath = filePath;

        if (Settings.ShowFileSizeInPreviewPanel)
        {
            FileSize = type == ResultType.File ? GetFileSize(filePath) : GetFolderSize(filePath);
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
        catch (Exception e)
        {
            Main.Context.API.LogException(ClassName, $"Failed to get file modified date for {filePath}", e);
            return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
        }
    }

    public static string GetFolderSize(string folderPath)
    {
        try
        {
            var directoryInfo = new DirectoryInfo(folderPath);
            long size = 0;
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Timeout occurred, return unknown size
                    cancellationTokenSource.Dispose();
                    return Main.Context.API.GetTranslation("plugin_explorer_plugin_tooltip_more_info_unknown");
                }
                size += file.Length;
            }
            return ResultManager.ToReadableSize(size, 2);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
