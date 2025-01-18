using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Image;
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
            CreatedAt = File
                .GetCreationTime(filePath)
                .ToString(
                    $"{Settings.PreviewPanelDateFormat} {Settings.PreviewPanelTimeFormat}",
                    CultureInfo.CurrentCulture
                );
        }

        if (Settings.ShowModifiedDateInPreviewPanel)
        {
            LastModifiedAt = File
                .GetLastWriteTime(filePath)
                .ToString(
                    $"{Settings.PreviewPanelDateFormat} {Settings.PreviewPanelTimeFormat}",
                    CultureInfo.CurrentCulture
                );
        }

        _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        PreviewImage = await ImageLoader.LoadAsync(FilePath, true).ConfigureAwait(false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
