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
            var fileSize = new FileInfo(filePath).Length;
            FileSize = ResultManager.ToReadableSize(fileSize, 2);
        }

        if (Settings.ShowCreatedDateInPreviewPanel)
        {
            DateTime createdDate = File.GetCreationTime(filePath);
            string formattedDate = createdDate.ToString(
                $"{Settings.PreviewPanelDateFormat} {Settings.PreviewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            string result = formattedDate;
            if (Settings.ShowRelativeDateInPreviewPanel) result = $"{GetRelativeDate(createdDate)} - {formattedDate}";
            CreatedAt = result;
        }

        if (Settings.ShowModifiedDateInPreviewPanel)
        {
            DateTime lastModifiedDate = File.GetLastWriteTime(filePath);
            string formattedDate = lastModifiedDate.ToString(
                $"{Settings.PreviewPanelDateFormat} {Settings.PreviewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );
            string result = formattedDate;
            if (Settings.ShowRelativeDateInPreviewPanel) result = $"{GetRelativeDate(lastModifiedDate)} - {formattedDate}";
            LastModifiedAt = result;
        }

        _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        PreviewImage = await Main.Context.API.LoadImageAsync(FilePath, true).ConfigureAwait(false);
    }
    
    private string GetRelativeDate(DateTime fileDateTime)
    {
        DateTime now = DateTime.Now;
        TimeSpan difference = now - fileDateTime;

        if (difference.TotalDays < 1)
            return "Today";
        if (difference.TotalDays < 30)
            return $"{(int)difference.TotalDays} days ago";

        int monthsDiff = (now.Year - fileDateTime.Year) * 12 + now.Month - fileDateTime.Month;
        if (monthsDiff < 12)
            return monthsDiff == 1 ? "1 month ago" : $"{monthsDiff} months ago";

        int yearsDiff = now.Year - fileDateTime.Year;
        if (now.Month < fileDateTime.Month || (now.Month == fileDateTime.Month && now.Day < fileDateTime.Day))
            yearsDiff--; 

        return yearsDiff == 1 ? "1 year ago" : $"{yearsDiff} years ago";
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
