using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Storage;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// ViewModel for a single result item.
/// </summary>
public partial class ResultViewModel : ObservableObject
{
    private static readonly string ClassName = nameof(ResultViewModel);

    private const string DefaultProgressBarColor = "#26a0da";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subTitle = string.Empty;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Settings? _settings;

    [ObservableProperty]
    private int _score;

    [ObservableProperty]
    private IList<int>? _titleHighlightData;

    private Result? _pluginResult;

    /// <summary>
    /// The underlying plugin result. Used for executing actions and accessing additional properties.
    /// </summary>
    public Result? PluginResult
    {
        get => _pluginResult;
        set
        {
            if (ReferenceEquals(_pluginResult, value))
                return;

            _pluginResult = value;
            ResetImages();
            _customPreviewResolved = false;
            _customPreviewControl = null;
            // Every result-derived property may have changed.
            OnPropertyChanged(string.Empty);
        }
    }

    /// <summary>
    /// Backing history item when this row represents an item from the history view.
    /// </summary>
    public LastOpenedHistoryResult? HistoryItem { get; set; }

    // Computed properties for display
    public bool ShowIcon => !string.IsNullOrEmpty(IconPath) || HasIconDelegate;

    public bool ShowSubTitle => !string.IsNullOrEmpty(SubTitle);

    /// <summary>
    /// Gets the query suggestion text for autocomplete, if available.
    /// </summary>
    public string? QuerySuggestionText => PluginResult?.AutoCompleteText;

    /// <summary>Tooltip of the title: the plugin's <see cref="Result.TitleToolTip"/>, else the title.</summary>
    public string TitleToolTip => string.IsNullOrEmpty(PluginResult?.TitleToolTip) ? Title : PluginResult.TitleToolTip;

    /// <summary>Tooltip of the subtitle: the plugin's <see cref="Result.SubTitleToolTip"/>, else the subtitle.</summary>
    public string SubTitleToolTip => string.IsNullOrEmpty(PluginResult?.SubTitleToolTip) ? SubTitle : PluginResult.SubTitleToolTip;

    /// <summary>Pixel width list icons are decoded to (32 DIP slot at 2x scale).</summary>
    private const int ListIconDecodeWidth = 64;

    /// <summary>Pixel width badge icons are decoded to (~19 DIP slot at 2x scale).</summary>
    private const int BadgeIconDecodeWidth = 40;

    // Image tasks are created lazily and cached until the icon source (IconPath / PluginResult) changes.
    private Task<IImage?>? _imageTask;
    private Task<IImage?>? _listIconTask;
    private Task<IImage?>? _badgeImageTask;
    private Task<IImage?>? _previewImageTask;

    /// <summary>
    /// The full-size icon image task (used by the preview panel). Use with Avalonia's ^ stream binding operator.
    /// Returns a cached task to avoid re-loading on every property access.
    /// </summary>
    public Task<IImage?> Image => _imageTask ??= LoadIcon(ResolvedIconPath, PluginResult?.Icon, 0);

    /// <summary>
    /// The list-sized icon image task. Use with Avalonia's ^ stream binding operator.
    /// </summary>
    public Task<IImage?> ListIcon => _listIconTask ??= LoadIcon(ResolvedIconPath, PluginResult?.Icon, ListIconDecodeWidth);

    /// <summary>
    /// Icon path with plugin-relative paths resolved against the plugin directory
    /// (<see cref="Result.IcoPathAbsolute"/> when the row shows the result's own icon).
    /// </summary>
    private string ResolvedIconPath =>
        PluginResult is { } result && !string.IsNullOrEmpty(result.IcoPathAbsolute) && string.Equals(IconPath, result.IcoPath, StringComparison.Ordinal)
            ? result.IcoPathAbsolute
            : IconPath;

    /// <summary>
    /// The plugin supplied no icon path but an icon delegate (WPF: the delegate is only used when IcoPath is empty).
    /// </summary>
    private bool HasIconDelegate => PluginResult is { Icon: not null } result && string.IsNullOrEmpty(result.IcoPath);

    partial void OnIconPathChanged(string value)
    {
        ResetImages();
        OnPropertyChanged(nameof(ShowIcon));
        OnPropertyChanged(nameof(ShowGlyph));
        OnPropertyChanged(nameof(ShowPreviewGlyph));
        OnPropertyChanged(nameof(ShowPreviewImage));
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(TitleToolTip));

    partial void OnSubTitleChanged(string value)
    {
        OnPropertyChanged(nameof(ShowSubTitle));
        OnPropertyChanged(nameof(SubTitleToolTip));
        OnPropertyChanged(nameof(PreviewDescription));
        OnPropertyChanged(nameof(ShowPreviewDescription));
        OnPropertyChanged(nameof(PreviewMarkdown));
        OnPropertyChanged(nameof(ShowPreviewFilePath));
    }

    partial void OnSettingsChanged(Settings? value)
    {
        OnPropertyChanged(nameof(ShowGlyph));
        OnPropertyChanged(nameof(ShowPreviewGlyph));
        OnPropertyChanged(nameof(ShowPreviewImage));
        OnPropertyChanged(nameof(ShowBadge));
        OnPropertyChanged(nameof(BadgeImage));
        OnPropertyChanged(nameof(TitleFontWeight));
        OnPropertyChanged(nameof(TitleFontStyle));
        OnPropertyChanged(nameof(TitleFontStretch));
        OnPropertyChanged(nameof(SubTitleFontWeight));
        OnPropertyChanged(nameof(SubTitleFontStyle));
        OnPropertyChanged(nameof(SubTitleFontStretch));
    }

    private void ResetImages()
    {
        _imageTask = null;
        _listIconTask = null;
        _badgeImageTask = null;
        _previewImageTask = null;
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(ListIcon));
        OnPropertyChanged(nameof(BadgeImage));
        OnPropertyChanged(nameof(PreviewImage));
    }

    /// <summary>
    /// Loads <paramref name="path"/>, or calls the plugin's icon delegate when no path is given.
    /// </summary>
    private static Task<IImage?> LoadIcon(string? path, Result.IconDelegate? iconDelegate, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path) && iconDelegate != null)
            return ImageLoader.LoadFromDelegateAsync(() => iconDelegate(), decodeWidth);

        return ImageLoader.LoadAsync(path, decodeWidth);
    }

    // Rounded icon / badge / progress bar

    /// <summary>Corner radius clipping the list icon: a circle when the result asks for <see cref="Result.RoundedIcon"/>.</summary>
    public CornerRadius IconCornerRadius => PluginResult?.RoundedIcon == true ? new CornerRadius(16) : new CornerRadius(0);

    /// <summary>Badge path; results without their own badge fall back to the plugin icon (WPF MainViewModel behaviour).</summary>
    private string? BadgeIconPath
    {
        get
        {
            if (PluginResult is not { } result)
                return null;

            if (!string.IsNullOrEmpty(result.BadgeIcoPath))
                return result.BadgeIcoPath;

            if (result.BadgeIcon != null || string.IsNullOrEmpty(result.PluginID))
                return null;

            return PluginManager.GetPluginForId(result.PluginID)?.Metadata.IcoPath;
        }
    }

    private bool BadgeIconAvailable => !string.IsNullOrEmpty(BadgeIconPath) || PluginResult?.BadgeIcon != null;

    private bool IsGlobalQuery => string.IsNullOrEmpty(PluginResult?.OriginQuery?.ActionKeyword);

    /// <summary>
    /// Whether the plugin badge overlays the icon: the result opts in, the user enabled badges
    /// (optionally only for global queries) and a badge image exists.
    /// </summary>
    public bool ShowBadge
    {
        get
        {
            if (PluginResult is not { ShowBadge: true } || Settings is not { ShowBadges: true } || !BadgeIconAvailable)
                return false;

            return !Settings.ShowBadgesGlobalOnly || IsGlobalQuery;
        }
    }

    /// <summary>The badge image task (no load while the badge is hidden). Use with Avalonia's ^ stream binding operator.</summary>
    public Task<IImage?> BadgeImage => ShowBadge
        ? (_badgeImageTask ??= LoadIcon(BadgeIconPath, PluginResult?.BadgeIcon, BadgeIconDecodeWidth))
        : NoImage;

    private static readonly Task<IImage?> NoImage = Task.FromResult<IImage?>(null);

    public bool ShowProgressBar => PluginResult?.ProgressBar != null;

    public int ResultProgress => PluginResult?.ProgressBar ?? 0;

    public IBrush ProgressBarBrush =>
        new SolidColorBrush(PluginResult?.ProgressBarColor is { } value && Color.TryParse(value.Trim(), out var color)
            ? color
            : Color.Parse(DefaultProgressBarColor));

    // Fonts (Settings stores WPF typeface names such as "Bold", "Italic", "Condensed")

    public FontWeight TitleFontWeight => ParseEnum(Settings?.ResultFontWeight, FontWeight.Normal);
    public FontStyle TitleFontStyle => ParseEnum(Settings?.ResultFontStyle, FontStyle.Normal);
    public FontStretch TitleFontStretch => ParseEnum(Settings?.ResultFontStretch, FontStretch.Normal);
    public FontWeight SubTitleFontWeight => ParseEnum(Settings?.ResultSubFontWeight, FontWeight.Normal);
    public FontStyle SubTitleFontStyle => ParseEnum(Settings?.ResultSubFontStyle, FontStyle.Normal);
    public FontStretch SubTitleFontStretch => ParseEnum(Settings?.ResultSubFontStretch, FontStretch.Normal);

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value?.Trim(), true, out var parsed) ? parsed : fallback;

    // Preview panel

    private Result.PreviewInfo? Preview => PluginResult?.Preview;

    private bool PreviewImageAvailable => !string.IsNullOrEmpty(Preview?.PreviewImagePath) || Preview?.PreviewDelegate != null;

    /// <summary>Preview image: the plugin's preview image/delegate, else the full-size result icon.</summary>
    public Task<IImage?> PreviewImage => _previewImageTask ??= PreviewImageAvailable
        ? LoadIcon(ResolvePluginPath(Preview!.PreviewImagePath), Preview.PreviewDelegate, 0)
        : Image;

    public bool ShowPreviewImage => PreviewImageAvailable || !ShowGlyph;

    public bool ShowPreviewGlyph => ShowGlyph && !PreviewImageAvailable;

    /// <summary>Media previews use the full panel width instead of an icon-sized thumbnail.</summary>
    public bool UseBigThumbnail => Preview?.IsMedia == true;

    public double PreviewImageMaxWidth => UseBigThumbnail ? double.PositiveInfinity : 96;

    public string PreviewDescription => Preview?.Description ?? SubTitle;

    public bool ShowPreviewDescription => !string.IsNullOrEmpty(PreviewDescription);

    public bool IsMarkdownPreview => Preview?.ContentType == PreviewContentType.Markdown;

    /// <summary>Markdown source of the preview; empty for non-markdown results so the viewer doesn't parse them.</summary>
    public string PreviewMarkdown => IsMarkdownPreview ? PreviewDescription : string.Empty;

    public string? PreviewFilePath => Preview?.FilePath;

    public bool ShowPreviewFilePath => !string.IsNullOrEmpty(PreviewFilePath) && PreviewFilePath != PreviewDescription;

    private bool _customPreviewResolved;
    private Control? _customPreviewControl;

    /// <summary>
    /// The plugin's own preview control (<see cref="Result.PreviewPanel"/>) when it is an Avalonia control.
    /// Created on first access; WPF-only panels are ignored so the default preview is shown instead.
    /// </summary>
    public Control? CustomPreviewControl
    {
        get
        {
            if (_customPreviewResolved)
                return _customPreviewControl;

            _customPreviewResolved = true;
            // Typed as object: on Windows PreviewPanel is Lazy<System.Windows.Controls.UserControl>.
            object? panel = PluginResult?.PreviewPanel;
            if (panel is Lazy<object> lazy)
            {
                try
                {
                    _customPreviewControl = lazy.Value as Control;
                }
                catch (Exception e)
                {
                    Log.Exception(ClassName, $"Failed to create the preview panel of result <{Title}>", e);
                }
            }

            return _customPreviewControl;
        }
    }

    public bool ShowCustomPreview => CustomPreviewControl != null;

    public bool ShowDefaultPreview => !ShowCustomPreview;

    private string? ResolvePluginPath(string? path)
    {
        var pluginDirectory = PluginResult?.PluginDirectory;
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(pluginDirectory) || System.IO.Path.IsPathRooted(path)
            || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            return path;

        return System.IO.Path.Combine(pluginDirectory, path);
    }

    // Glyph support
    private GlyphInfo? _glyph;

    public GlyphInfo? Glyph
    {
        get => _glyph;
        set
        {
            if (SetProperty(ref _glyph, value))
            {
                OnPropertyChanged(nameof(GlyphAvailable));
                OnPropertyChanged(nameof(ShowGlyph));
                OnPropertyChanged(nameof(ShowPreviewGlyph));
                OnPropertyChanged(nameof(ShowPreviewImage));
                OnPropertyChanged(nameof(GlyphFontFamily));
            }
        }
    }

    public bool GlyphAvailable => Glyph != null;

    public bool ShowGlyph => GlyphAvailable && (Settings?.UseGlyphIcons == true || !ShowIcon);

    /// <summary>
    /// Gets the FontFamily for the glyph icon, handling file paths and resource paths.
    /// </summary>
    public FontFamily? GlyphFontFamily => Glyph != null ? FontLoader.GetFontFamily(Glyph) : null;
}
