using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using AvaloniaI18n = Flow.Launcher.Avalonia.Resource.Internationalization;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ThemeSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Settings _settings;
    private readonly AvaloniaI18n _i18n;
    private readonly System.ComponentModel.PropertyChangedEventHandler _settingsPropertyChangedHandler;

    private const string ThemeMetadataNamePrefix = "Name:";
    private const string ThemeMetadataIsDarkPrefix = "IsDark:";
    private const string ThemeMetadataHasBlurPrefix = "HasBlur:";

    public ThemeSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<AvaloniaI18n>();

        ColorSchemeOptions = DropdownDataGeneric<ColorSchemes>.GetEnumData("ColorScheme");
        BackdropTypesList = DropdownDataGeneric<BackdropTypes>.GetEnumData("BackdropTypes");
        AnimationSpeedOptions = DropdownDataGeneric<AnimationSpeeds>.GetEnumData("AnimationSpeed");
        AvailableFonts = SystemFontCache.FontNames;
        Themes = LoadThemes();
        CodeHighlightThemeOptions = DropdownDataGeneric<CodeHighlightThemes>.GetEnumData("CodeHighlightTheme");
        QueryTypefaceOptions = GetTypefaceOptions(_settings.QueryBoxFont);
        ResultTypefaceOptions = GetTypefaceOptions(_settings.ResultFont);
        ResultSubTypefaceOptions = GetTypefaceOptions(_settings.ResultSubFont);

        _settingsPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.Language))
            {
                UpdateLabels();
            }
        };
        _settings.PropertyChanged += _settingsPropertyChangedHandler;

        // Keeps the clock/date in the preview and the format rows live.
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    private readonly DispatcherTimer _clockTimer;

    public List<DropdownDataGeneric<ColorSchemes>> ColorSchemeOptions { get; }

    public List<DropdownDataGeneric<BackdropTypes>> BackdropTypesList { get; }

    public List<DropdownDataGeneric<AnimationSpeeds>> AnimationSpeedOptions { get; }

    public IReadOnlyList<string> AvailableFonts { get; }

    public List<ThemeData> Themes { get; }

    public ThemeData? SelectedTheme
    {
        get => Themes.FirstOrDefault(theme => theme.FileNameWithoutExtension == _settings.Theme) ?? Themes.FirstOrDefault();
        set
        {
            if (value == null || _settings.Theme == value.FileNameWithoutExtension)
            {
                return;
            }

            if (App.API?.SetCurrentTheme(value) == true)
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBackdropEnabled));
                OnPropertyChanged(nameof(IsDropShadowEnabled));
            }
        }
    }

    public ColorSchemes SelectedColorScheme
    {
        get => ThemeLoader.ParseColorScheme(_settings.ColorScheme);
        set
        {
            if (SelectedColorScheme == value)
            {
                return;
            }

            _settings.ColorScheme = value.ToString();
            if (Application.Current is { } application)
            {
                ThemeLoader.ApplyColorScheme(application, value);
            }

            OnPropertyChanged();
        }
    }

    public string BackdropSubText => !Win32Helper.IsBackdropSupported() ? Translate("BackdropTypeDisabledToolTip", "Backdrop supported starting from Windows 11 build 22000 and above") : string.Empty;

    public bool IsBackdropEnabled => Win32Helper.IsBackdropSupported();

    public BackdropTypes SelectedBackdropType
    {
        get => _settings.BackdropType;
        set
        {
            if (_settings.BackdropType == value)
            {
                return;
            }

            _settings.BackdropType = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Blur themes draw no drop shadow (WPF IsDropShadowEnabled), so the toggle is disabled for them.</summary>
    public bool IsDropShadowEnabled => SelectedTheme?.HasBlur != true;

    public bool DropShadowEffect
    {
        get => _settings.UseDropShadowEffect;
        set
        {
            if (_settings.UseDropShadowEffect == value)
            {
                return;
            }

            // ThemeResources (Helper/ThemeLoader.cs) applies the change to the window shadow live.
            _settings.UseDropShadowEffect = value;
            OnPropertyChanged();
        }
    }

    public bool KeepMaxResults
    {
        get => _settings.KeepMaxResults;
        set
        {
            _settings.KeepMaxResults = value;
            OnPropertyChanged();
        }
    }

    public int MaxResults
    {
        get => _settings.MaxResultsToShow;
        set
        {
            _settings.MaxResultsToShow = value;
            OnPropertyChanged();
        }
    }

    public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public double WindowHeightSize
    {
        get => _settings.WindowHeightSize;
        set
        {
            _settings.WindowHeightSize = value;
            OnPropertyChanged();
        }
    }

    public double ItemHeightSize
    {
        get => _settings.ItemHeightSize;
        set
        {
            _settings.ItemHeightSize = value;
            OnPropertyChanged();
        }
    }

    public double QueryBoxFontSize
    {
        get => _settings.QueryBoxFontSize;
        set
        {
            _settings.QueryBoxFontSize = value;
            OnPropertyChanged();
        }
    }

    public double ResultItemFontSize
    {
        get => _settings.ResultItemFontSize;
        set
        {
            _settings.ResultItemFontSize = value;
            OnPropertyChanged();
        }
    }

    public double ResultSubItemFontSize
    {
        get => _settings.ResultSubItemFontSize;
        set
        {
            _settings.ResultSubItemFontSize = value;
            OnPropertyChanged();
        }
    }

    public string QueryFont
    {
        get => _settings.QueryBoxFont;
        set
        {
            if (_settings.QueryBoxFont == value)
            {
                return;
            }

            _settings.QueryBoxFont = value;
            QueryTypefaceOptions = GetTypefaceOptions(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(QueryTypefaceOptions));
            SelectedQueryTypeface = FindTypefaceOption(QueryTypefaceOptions, _settings.QueryBoxFontStyle, _settings.QueryBoxFontWeight, _settings.QueryBoxFontStretch);
        }
    }

    public string ResultFont
    {
        get => _settings.ResultFont;
        set
        {
            if (_settings.ResultFont == value)
            {
                return;
            }

            _settings.ResultFont = value;
            ResultTypefaceOptions = GetTypefaceOptions(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultTypefaceOptions));
            SelectedResultTypeface = FindTypefaceOption(ResultTypefaceOptions, _settings.ResultFontStyle, _settings.ResultFontWeight, _settings.ResultFontStretch);
        }
    }

    public string ResultSubFont
    {
        get => _settings.ResultSubFont;
        set
        {
            if (_settings.ResultSubFont == value)
            {
                return;
            }

            _settings.ResultSubFont = value;
            ResultSubTypefaceOptions = GetTypefaceOptions(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultSubTypefaceOptions));
            SelectedResultSubTypeface = FindTypefaceOption(ResultSubTypefaceOptions, _settings.ResultSubFontStyle, _settings.ResultSubFontWeight, _settings.ResultSubFontStretch);
        }
    }

    public List<FontTypefaceOption> QueryTypefaceOptions { get; private set; }

    // Setting a face the new family lacks snaps to the closest available one (FindTypefaceOption fallback) and persists it.
    public FontTypefaceOption? SelectedQueryTypeface
    {
        get => FindTypefaceOption(QueryTypefaceOptions, _settings.QueryBoxFontStyle, _settings.QueryBoxFontWeight, _settings.QueryBoxFontStretch);
        set
        {
            if (value == null)
            {
                return;
            }

            _settings.QueryBoxFontStyle = value.Style;
            _settings.QueryBoxFontWeight = value.Weight;
            _settings.QueryBoxFontStretch = value.Stretch;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QueryFontWeight));
            OnPropertyChanged(nameof(QueryFontStyle));
        }
    }

    public List<FontTypefaceOption> ResultTypefaceOptions { get; private set; }

    public FontTypefaceOption? SelectedResultTypeface
    {
        get => FindTypefaceOption(ResultTypefaceOptions, _settings.ResultFontStyle, _settings.ResultFontWeight, _settings.ResultFontStretch);
        set
        {
            if (value == null)
            {
                return;
            }

            _settings.ResultFontStyle = value.Style;
            _settings.ResultFontWeight = value.Weight;
            _settings.ResultFontStretch = value.Stretch;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultFontWeight));
            OnPropertyChanged(nameof(ResultFontStyle));
        }
    }

    public List<FontTypefaceOption> ResultSubTypefaceOptions { get; private set; }

    public FontTypefaceOption? SelectedResultSubTypeface
    {
        get => FindTypefaceOption(ResultSubTypefaceOptions, _settings.ResultSubFontStyle, _settings.ResultSubFontWeight, _settings.ResultSubFontStretch);
        set
        {
            if (value == null)
            {
                return;
            }

            _settings.ResultSubFontStyle = value.Style;
            _settings.ResultSubFontWeight = value.Weight;
            _settings.ResultSubFontStretch = value.Stretch;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultSubFontWeight));
            OnPropertyChanged(nameof(ResultSubFontStyle));
        }
    }

    // Preview font faces resolved from the persisted typeface strings.
    public FontWeight QueryFontWeight => ParseFontWeight(_settings.QueryBoxFontWeight);
    public FontStyle QueryFontStyle => ParseFontStyle(_settings.QueryBoxFontStyle);
    public FontWeight ResultFontWeight => ParseFontWeight(_settings.ResultFontWeight);
    public FontStyle ResultFontStyle => ParseFontStyle(_settings.ResultFontStyle);
    public FontWeight ResultSubFontWeight => ParseFontWeight(_settings.ResultSubFontWeight);
    public FontStyle ResultSubFontStyle => ParseFontStyle(_settings.ResultSubFontStyle);

    private static FontWeight ParseFontWeight(string? value) =>
        Enum.TryParse<FontWeight>(value, true, out var weight) ? weight : FontWeight.Normal;

    private static FontStyle ParseFontStyle(string? value) =>
        Enum.TryParse<FontStyle>(value, true, out var style) ? style : FontStyle.Normal;

    public bool UseGlyphIcons
    {
        get => _settings.UseGlyphIcons;
        set
        {
            _settings.UseGlyphIcons = value;
            OnPropertyChanged();
        }
    }

    public bool UseClock
    {
        get => _settings.UseClock;
        set
        {
            _settings.UseClock = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ClockText));
        }
    }

    public bool UseDate
    {
        get => _settings.UseDate;
        set
        {
            _settings.UseDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DateText));
        }
    }

    public List<string> TimeFormatList { get; } =
    [
        "h:mm",
        "hh:mm",
        "H:mm",
        "HH:mm",
        "tt h:mm",
        "tt hh:mm",
        "h:mm tt",
        "hh:mm tt",
        "hh:mm:ss tt",
        "HH:mm:ss"
    ];

    public List<string> DateFormatList { get; } =
    [
        "MM'/'dd dddd",
        "MM'/'dd ddd",
        "MM'/'dd",
        "MM'-'dd",
        "MMMM', 'dd",
        "dd'/'MM",
        "dd'-'MM",
        "ddd MM'/'dd",
        "dddd MM'/'dd",
        "dddd",
        "ddd dd'/'MM",
        "dddd dd'/'MM",
        "dddd dd', 'MMMM",
        "dd', 'MMMM",
        "dd.MM.yy",
        "dd.MM.yyyy",
        "dd MMMM yyyy",
        "yyyy-MM-dd"
    ];

    public string TimeFormat
    {
        get => _settings.TimeFormat;
        set
        {
            _settings.TimeFormat = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ClockText));
        }
    }

    public string DateFormat
    {
        get => _settings.DateFormat;
        set
        {
            _settings.DateFormat = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DateText));
        }
    }

    public string ClockText => DateTime.Now.ToString(TimeFormat, CultureInfo.CurrentUICulture);

    public string DateText => DateTime.Now.ToString(DateFormat, CultureInfo.CurrentUICulture);

    /// <summary>Text shown in the preview's empty query box: the custom placeholder, or the default one when blank.</summary>
    public string PreviewPlaceholderText => string.IsNullOrEmpty(PlaceholderText) ? Translate("queryTextBoxPlaceholder", "Type here to search") : PlaceholderText;

    public string ClockAndDateText => $"{Translate("Clock", "Clock")} / {Translate("Date", "Date")}";

    public bool ShowPlaceholder
    {
        get => _settings.ShowPlaceholder;
        set
        {
            _settings.ShowPlaceholder = value;
            OnPropertyChanged();
        }
    }

    public string PlaceholderText
    {
        get => _settings.PlaceholderText;
        set
        {
            _settings.PlaceholderText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewPlaceholderText));
        }
    }

    public string PlaceholderTextTip => string.Format(Translate("PlaceholderTextTip", "Change placeholder text. Input empty will use: {0}"), Translate("queryTextBoxPlaceholder", "Type here to search"));

    public bool UseAnimation
    {
        get => _settings.UseAnimation;
        set
        {
            _settings.UseAnimation = value;
            OnPropertyChanged();
        }
    }

    public AnimationSpeeds SelectedAnimationSpeed
    {
        get => _settings.AnimationSpeed;
        set
        {
            _settings.AnimationSpeed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomAnimationSpeed));
        }
    }

    public bool IsCustomAnimationSpeed => SelectedAnimationSpeed == AnimationSpeeds.Custom;

    public int CustomAnimationLength
    {
        get => _settings.CustomAnimationLength;
        set
        {
            _settings.CustomAnimationLength = value;
            OnPropertyChanged();
        }
    }

    public bool UseSound
    {
        get => _settings.UseSound;
        set
        {
            _settings.UseSound = value;
            OnPropertyChanged();
        }
    }

    public double SoundEffectVolume
    {
        get => _settings.SoundVolume;
        set
        {
            _settings.SoundVolume = value;
            OnPropertyChanged();
        }
    }

    public bool ShowBadges
    {
        get => _settings.ShowBadges;
        set
        {
            _settings.ShowBadges = value;
            OnPropertyChanged();
        }
    }

    public List<DropdownDataGeneric<CodeHighlightThemes>> CodeHighlightThemeOptions { get; }

    public CodeHighlightThemes SelectedCodeHighlightTheme
    {
        get => Enum.TryParse<CodeHighlightThemes>(_settings.CodeHighlightTheme, true, out var theme) ? theme : CodeHighlightThemes.Auto;
        set
        {
            if (SelectedCodeHighlightTheme == value)
            {
                return;
            }

            _settings.CodeHighlightTheme = value.ToString();
            OnPropertyChanged();
        }
    }

    public bool ShowBadgesGlobalOnly
    {
        get => _settings.ShowBadgesGlobalOnly;
        set
        {
            _settings.ShowBadgesGlobalOnly = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void OpenThemesFolder()
    {
        App.API?.OpenDirectory(DataLocation.ThemesDirectory);
    }

    [RelayCommand]
    private void OpenThemeGallery()
    {
        Process.Start(new ProcessStartInfo("https://github.com/Flow-Launcher/Flow.Launcher/discussions/1438") { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenThemeBuilder()
    {
        Process.Start(new ProcessStartInfo("https://www.flowlauncher.com/theme-builder/") { UseShellExecute = true });
    }

    [RelayCommand]
    private void ResetSizing()
    {
        QueryBoxFontSize = 16;
        ResultItemFontSize = 16;
        ResultSubItemFontSize = 13;
        WindowHeightSize = 42;
        ItemHeightSize = 58;
        QueryFont = Settings.GetSystemDefaultFont(true);
        ResultFont = Settings.GetSystemDefaultFont(true);
        ResultSubFont = Settings.GetSystemDefaultFont(true);
    }

    [RelayCommand]
    private void ImportSizing()
    {
        if (SelectedTheme == null)
        {
            return;
        }

        var themePath = ThemeLoader.GetThemePath(SelectedTheme.FileNameWithoutExtension);
        if (string.IsNullOrWhiteSpace(themePath) || !File.Exists(themePath))
        {
            return;
        }

        ImportSizingFromTheme(themePath);
    }

    // Windows reads the theme through WPF (ThemeSettingsViewModel.Windows.cs); other platforms parse the XAML (ThemeSettingsViewModel.CrossPlatform.cs).
    partial void ImportSizingFromTheme(string themePath);

    private void UpdateLabels()
    {
        ColorSchemeOptions.ForEach(x => x.UpdateLabels());
        BackdropTypesList.ForEach(x => x.UpdateLabels());
        AnimationSpeedOptions.ForEach(x => x.UpdateLabels());
        CodeHighlightThemeOptions.ForEach(x => x.UpdateLabels());
        OnPropertyChanged(nameof(ClockAndDateText));
        OnPropertyChanged(nameof(PreviewPlaceholderText));
    }

    private List<ThemeData> LoadThemes()
    {
        var themeDirectories = new[]
        {
            Path.Combine(Constant.ProgramDirectory, Constant.Themes),
            Path.Combine(DataLocation.DataDirectory(), Constant.Themes)
        };

        var themes = new List<ThemeData>();
        foreach (var directory in themeDirectories.Where(Directory.Exists))
        {
            foreach (var path in Directory.GetFiles(directory, "*.xaml")
                .Where(path => !path.EndsWith("Base.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    themes.Add(GetThemeDataFromPath(path));
                }
                catch (Exception ex)
                {
                    Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(ThemeSettingsViewModel), $"Failed to load theme metadata from {path}", ex);
                }
            }
        }

        return themes.OrderBy(theme => theme.Name).ToList();
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= _settingsPropertyChangedHandler;
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTick;
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ClockText));
        OnPropertyChanged(nameof(DateText));
    }

    // Names are explicit because FontWeight has aliased values (Normal/Regular, ExtraLight/UltraLight, ...).
    private static readonly (FontWeight Weight, string Name)[] ProbedWeights =
    [
        (FontWeight.Thin, "Thin"), (FontWeight.ExtraLight, "ExtraLight"), (FontWeight.Light, "Light"),
        (FontWeight.SemiLight, "SemiLight"), (FontWeight.Normal, "Normal"), (FontWeight.Medium, "Medium"),
        (FontWeight.SemiBold, "SemiBold"), (FontWeight.Bold, "Bold"), (FontWeight.ExtraBold, "ExtraBold"),
        (FontWeight.Black, "Black"), (FontWeight.ExtraBlack, "ExtraBlack")
    ];

    private static readonly FontStyle[] ProbedStyles = [FontStyle.Normal, FontStyle.Italic, FontStyle.Oblique];

    // Used when the family's faces cannot be enumerated (unknown family or font manager unavailable).
    private static readonly List<FontTypefaceOption> FallbackTypefaceOptions =
    [
        new FontTypefaceOption("Normal", "Normal", "Normal", "Normal"),
        new FontTypefaceOption("Bold", "Normal", "Bold", "Normal"),
        new FontTypefaceOption("Italic", "Italic", "Normal", "Normal"),
        new FontTypefaceOption("Bold Italic", "Italic", "Bold", "Normal")
    ];

    /// <summary>Real (non-simulated) weight/style faces the font family provides, like WPF FontFamily.FamilyTypefaces.</summary>
    private static List<FontTypefaceOption> GetTypefaceOptions(string? familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return FallbackTypefaceOptions;
        }

        var options = new List<FontTypefaceOption>();
        try
        {
            var family = new FontFamily(familyName);
            foreach (var style in ProbedStyles)
            {
                foreach (var (weight, weightName) in ProbedWeights)
                {
                    if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family, style, weight), out var glyphTypeface) ||
                        glyphTypeface.FontSimulations != FontSimulations.None ||
                        glyphTypeface.Weight != weight ||
                        glyphTypeface.Style != style)
                    {
                        continue;
                    }

                    var styleName = style.ToString();
                    var display = style == FontStyle.Normal ? weightName
                        : weight == FontWeight.Normal ? styleName
                        : $"{weightName} {styleName}";
                    options.Add(new FontTypefaceOption(display, styleName, weightName, nameof(FontStretch.Normal)));
                }
            }
        }
        catch (Exception ex)
        {
            Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(ThemeSettingsViewModel), $"Failed to enumerate typefaces of <{familyName}>", ex);
        }

        return options.Count > 0 ? options : FallbackTypefaceOptions;
    }

    private static FontTypefaceOption? FindTypefaceOption(IEnumerable<FontTypefaceOption> options, string? style, string? weight, string? stretch)
    {
        return options.FirstOrDefault(option =>
            string.Equals(option.Style, style, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.Weight, weight, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.Stretch, stretch, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
    }

    private static ThemeData GetThemeDataFromPath(string path)
    {
        using var reader = XmlReader.Create(path);
        reader.Read();

        var extensionlessName = Path.GetFileNameWithoutExtension(path);
        if (reader.NodeType is not XmlNodeType.Comment)
        {
            return new ThemeData(extensionlessName, extensionlessName);
        }

        var commentLines = reader.Value.Trim().Split('\n').Select(value => value.Trim());

        var name = extensionlessName;
        bool? isDark = null;
        bool? hasBlur = null;

        foreach (var line in commentLines)
        {
            if (line.StartsWith(ThemeMetadataNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                name = line[ThemeMetadataNamePrefix.Length..].Trim();
            }
            else if (line.StartsWith(ThemeMetadataIsDarkPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isDark = bool.Parse(line[ThemeMetadataIsDarkPrefix.Length..].Trim());
            }
            else if (line.StartsWith(ThemeMetadataHasBlurPrefix, StringComparison.OrdinalIgnoreCase))
            {
                hasBlur = bool.Parse(line[ThemeMetadataHasBlurPrefix.Length..].Trim());
            }
        }

        return new ThemeData(extensionlessName, name, isDark, hasBlur);
    }

    private string Translate(string key, string fallback)
    {
        var value = _i18n.GetTranslation(key);
        return value.StartsWith('[') ? fallback : value;
    }

    public sealed record FontTypefaceOption(string Display, string Style, string Weight, string Stretch);
}
