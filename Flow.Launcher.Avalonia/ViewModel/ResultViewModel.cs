using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Avalonia.Helper;
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

    /// <summary>
    /// The underlying plugin result. Used for executing actions and accessing additional properties.
    /// </summary>
    public Result? PluginResult { get; set; }

    // Computed properties for display
    public bool ShowIcon => !string.IsNullOrEmpty(IconPath);
    
    public bool ShowSubTitle => !string.IsNullOrEmpty(SubTitle);

    /// <summary>
    /// Gets the query suggestion text for autocomplete, if available.
    /// </summary>
    public string? QuerySuggestionText => PluginResult?.AutoCompleteText;

    // Cached task for the image - created once per IconPath
    private Task<IImage?>? _imageTask;

    /// <summary>
    /// The icon image task. Use with Avalonia's ^ stream binding operator.
    /// Returns a cached task to avoid re-loading on every property access.
    /// </summary>
    public Task<IImage?> Image => _imageTask ??= ImageLoader.LoadAsync(IconPath);

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
                OnPropertyChanged(nameof(GlyphFontFamily));
            }
        }
    }

    public bool GlyphAvailable => Glyph != null;

    public bool ShowGlyph => 
        Settings?.UseGlyphIcons == true && GlyphAvailable;

    /// <summary>
    /// Gets the FontFamily for the glyph icon, handling file paths and resource paths.
    /// </summary>
    public FontFamily? GlyphFontFamily => Glyph != null ? FontLoader.GetFontFamily(Glyph) : null;
}
