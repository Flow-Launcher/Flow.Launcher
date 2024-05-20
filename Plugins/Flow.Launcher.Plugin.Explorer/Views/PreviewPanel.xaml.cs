using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    public string FileSize { get; }
    public string CreatedAt { get; }
    public string LastModifiedAt { get; }
    private ImageSource _previewImage = new BitmapImage();

    public ImageSource PreviewImage
    {
        get => _previewImage;
        private set
        {
            _previewImage = value;
            OnPropertyChanged();
        }
    }

    public PreviewPanel(string filePath)
    {
        InitializeComponent();

        FilePath = filePath;

        var fileSize = new FileInfo(filePath).Length;
        FileSize = ResultManager.ToReadableSize(fileSize, 2);

        DateTime created = File.GetCreationTime(filePath);
        CreatedAt = created.ToString("yy-M-dd ddd hh:mm", CultureInfo.CurrentCulture);

        DateTime lastModified = File.GetLastWriteTime(filePath);
        LastModifiedAt = lastModified.ToString("yy-M-dd ddd hh:mm", CultureInfo.CurrentCulture);

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
