using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// ViewModel for a single result item.
/// </summary>
public partial class ResultViewModel : ObservableObject
{
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
    private LastOpenedHistoryResult? _historyItem;
    private (string PluginId, string RecordKey, string Query, string Title, string SubTitle)? _identityKey;

    /// <summary>
    /// The underlying plugin result. Used for executing actions and accessing additional properties.
    /// </summary>
    public Result? PluginResult
    {
        get => _pluginResult;
        set
        {
            _pluginResult = value;
            _identityKey = null;
        }
    }

    /// <summary>
    /// Backing history item when this row represents an item from the history view.
    /// </summary>
    public LastOpenedHistoryResult? HistoryItem
    {
        get => _historyItem;
        set
        {
            _historyItem = value;
            _identityKey = null;
        }
    }

    /// <summary>
    /// Identity used to match rows across result updates; cached until one of its inputs changes.
    /// </summary>
    internal (string PluginId, string RecordKey, string Query, string Title, string SubTitle) IdentityKey =>
        _identityKey ??= (
            _pluginResult?.PluginID ?? _historyItem?.PluginID ?? string.Empty,
            _pluginResult?.RecordKey ?? _historyItem?.RecordKey ?? string.Empty,
            _historyItem?.Query ?? string.Empty,
            Title,
            SubTitle);

    partial void OnTitleChanged(string value) => _identityKey = null;

    partial void OnSubTitleChanged(string value) => _identityKey = null;

    // Computed properties for display
    public bool ShowIcon => !string.IsNullOrEmpty(IconPath);
    
    public bool ShowSubTitle => !string.IsNullOrEmpty(SubTitle);

    /// <summary>
    /// Gets the query suggestion text for autocomplete, if available.
    /// </summary>
    public string? QuerySuggestionText => PluginResult?.AutoCompleteText;

    /// <summary>Pixel width list icons are decoded to (32 DIP slot at 2x scale).</summary>
    private const int ListIconDecodeWidth = 64;

    // Cached task for the full-size image - created once per IconPath
    private Task<IImage?>? _imageTask;

    /// <summary>
    /// The full-size icon image task (used by the preview panel). Use with Avalonia's ^ stream binding operator.
    /// Returns a cached task to avoid re-loading on every property access.
    /// </summary>
    public Task<IImage?> Image => _imageTask ??= ImageLoader.LoadAsync(IconPath);

    private IImage? _icon;
    private bool _iconRequested;

    /// <summary>
    /// List-sized icon. Cached icons are returned synchronously; otherwise the default image is shown
    /// until the background load completes. Loading starts on first read, i.e. only for realized rows.
    /// </summary>
    public IImage? Icon
    {
        get
        {
            if (!_iconRequested)
            {
                _iconRequested = true;
                RequestIcon();
            }

            return _icon;
        }
    }

    private void RequestIcon()
    {
        var path = IconPath;
        if (ImageLoader.TryGetCached(path, ListIconDecodeWidth, out var cached))
        {
            _icon = cached;
            return;
        }

        var task = ImageLoader.LoadAsync(path, ListIconDecodeWidth);
        if (task.IsCompletedSuccessfully)
        {
            _icon = task.Result;
            return;
        }

        _icon = ImageLoader.DefaultImage;
        task.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
                return;

            var image = t.Result;
            Dispatcher.UIThread.Post(() =>
            {
                if (!_iconRequested || !string.Equals(IconPath, path, StringComparison.Ordinal))
                    return;

                _icon = image;
                OnPropertyChanged(nameof(Icon));
            });
        }, TaskScheduler.Default);
    }

    partial void OnIconPathChanged(string value)
    {
        _imageTask = null;
        _iconRequested = false;
        _icon = null;
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(ShowIcon));
        OnPropertyChanged(nameof(ShowGlyph));
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
                _glyphFontFamilyResolved = false;
                OnPropertyChanged(nameof(GlyphAvailable));
                OnPropertyChanged(nameof(ShowGlyph));
                OnPropertyChanged(nameof(GlyphFontFamily));
            }
        }
    }

    public bool GlyphAvailable => Glyph != null;

    public bool ShowGlyph => GlyphAvailable && (Settings?.UseGlyphIcons == true || !ShowIcon);

    private FontFamily? _glyphFontFamily;
    private bool _glyphFontFamilyResolved;

    /// <summary>
    /// Gets the FontFamily for the glyph icon, handling file paths and resource paths.
    /// Resolved once per Glyph value.
    /// </summary>
    public FontFamily? GlyphFontFamily
    {
        get
        {
            if (!_glyphFontFamilyResolved)
            {
                _glyphFontFamily = Glyph != null ? FontLoader.GetFontFamily(Glyph) : null;
                _glyphFontFamilyResolved = true;
            }

            return _glyphFontFamily;
        }
    }
}
