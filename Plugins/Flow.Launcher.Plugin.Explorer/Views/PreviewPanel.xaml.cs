using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
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

    public PreviewPanel(Settings settings, string filePath)
    {
        InitializeComponent();

        Settings = settings;

        FilePath = filePath;

        if (Settings.ShowFileSizeInPreviewPanel)
        {
            FileSize = GetFileSize(filePath);
        }

        if (Settings.ShowCreatedDateInPreviewPanel)
        {
            CreatedAt = GetCreatedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
        }

        if (Settings.ShowModifiedDateInPreviewPanel)
        {
            LastModifiedAt = GetLastModifiedAt(filePath, Settings.PreviewPanelDateFormat, Settings.PreviewPanelTimeFormat, Settings.ShowFileAgeInPreviewPanel);
        }

        _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        PreviewImage = await Main.Context.API.LoadImageAsync(FilePath, true).ConfigureAwait(false);
    }

    public static string GetFileSize(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return ResultManager.ToReadableSize(fileInfo.Length, 2);
    }

    public static string GetCreatedAt(string filePath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
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

    public static string GetLastModifiedAt(string filePath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
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
