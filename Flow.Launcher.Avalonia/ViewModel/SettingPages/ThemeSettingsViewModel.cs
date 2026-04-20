using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using WpfFrameworkElement = System.Windows.FrameworkElement;
using WpfResourceDictionary = System.Windows.ResourceDictionary;
using WpfSetter = System.Windows.Setter;
using WpfStyle = System.Windows.Style;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using AvaloniaI18n = Flow.Launcher.Avalonia.Resource.Internationalization;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ThemeSettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly AvaloniaI18n _i18n;

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
        AvailableFonts = FontManager.Current.SystemFonts.OrderBy(font => font.Name).Select(font => font.Name).Distinct().ToList();
        Themes = LoadThemes();

        ApplyColorScheme(SelectedColorScheme);

        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.Language))
            {
                UpdateLabels();
            }
        };
    }

    public List<DropdownDataGeneric<ColorSchemes>> ColorSchemeOptions { get; }

    public List<DropdownDataGeneric<BackdropTypes>> BackdropTypesList { get; }

    public List<DropdownDataGeneric<AnimationSpeeds>> AnimationSpeedOptions { get; }

    public List<string> AvailableFonts { get; }

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

            _settings.Theme = value.FileNameWithoutExtension;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBackdropEnabled));
        }
    }

    public ColorSchemes SelectedColorScheme
    {
        get => Enum.TryParse<ColorSchemes>(_settings.ColorScheme, true, out var result) ? result : ColorSchemes.System;
        set
        {
            if (SelectedColorScheme == value)
            {
                return;
            }

            _settings.ColorScheme = value.ToString();
            ApplyColorScheme(value);
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

    public bool DropShadowEffect
    {
        get => _settings.UseDropShadowEffect;
        set
        {
            if (_settings.UseDropShadowEffect == value)
            {
                return;
            }

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
            OnPropertyChanged();
            OnPropertyChanged(nameof(QueryTypefaceOptions));
            OnPropertyChanged(nameof(SelectedQueryTypeface));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultTypefaceOptions));
            OnPropertyChanged(nameof(SelectedResultTypeface));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultSubTypefaceOptions));
            OnPropertyChanged(nameof(SelectedResultSubTypeface));
        }
    }

    public List<FontTypefaceOption> QueryTypefaceOptions => GetTypefaceOptions(QueryFont);

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
        }
    }

    public List<FontTypefaceOption> ResultTypefaceOptions => GetTypefaceOptions(ResultFont);

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
        }
    }

    public List<FontTypefaceOption> ResultSubTypefaceOptions => GetTypefaceOptions(ResultSubFont);

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
        }
    }

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
            OnPropertyChanged(nameof(ShowWmpWarning));
            OnPropertyChanged(nameof(EnableVolumeAdjustment));
        }
    }

    public bool ShowWmpWarning => !_settings.WMPInstalled && UseSound;

    public bool EnableVolumeAdjustment => _settings.WMPInstalled;

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
        QueryFont = Win32Helper.GetSystemDefaultFont();
        ResultFont = Win32Helper.GetSystemDefaultFont();
        ResultSubFont = Win32Helper.GetSystemDefaultFont();
    }

    [RelayCommand]
    private void ImportSizing()
    {
        if (SelectedTheme == null)
        {
            return;
        }

        var themePath = GetThemePath(SelectedTheme.FileNameWithoutExtension);
        if (string.IsNullOrWhiteSpace(themePath) || !File.Exists(themePath))
        {
            return;
        }

        try
        {
            var resourceDictionary = new WpfResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Absolute)
            };

            if (resourceDictionary["QueryBoxStyle"] is WpfStyle queryBoxStyle)
            {
                if (TryGetSetterValue<double>(queryBoxStyle, WpfTextBox.FontSizeProperty, out var fontSize))
                {
                    QueryBoxFontSize = fontSize;
                }

                if (TryGetSetterValue<double>(queryBoxStyle, WpfFrameworkElement.HeightProperty, out var height))
                {
                    WindowHeightSize = height;
                }
            }

            if (resourceDictionary["ResultItemHeight"] is double itemHeight)
            {
                ItemHeightSize = itemHeight;
            }

            if (resourceDictionary["ItemTitleStyle"] is WpfStyle itemTitleStyle &&
                TryGetSetterValue<double>(itemTitleStyle, WpfTextBlock.FontSizeProperty, out var resultFontSize))
            {
                ResultItemFontSize = resultFontSize;
            }

            if (resourceDictionary["ItemSubTitleStyle"] is WpfStyle itemSubTitleStyle &&
                TryGetSetterValue<double>(itemSubTitleStyle, WpfTextBlock.FontSizeProperty, out var subResultFontSize))
            {
                ResultSubItemFontSize = subResultFontSize;
            }
        }
        catch (Exception)
        {
        }
    }

    private void UpdateLabels()
    {
        ColorSchemeOptions.ForEach(x => x.UpdateLabels());
        BackdropTypesList.ForEach(x => x.UpdateLabels());
        AnimationSpeedOptions.ForEach(x => x.UpdateLabels());
    }

    private void ApplyColorScheme(ColorSchemes scheme)
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = scheme switch
        {
            ColorSchemes.Light => ThemeVariant.Light,
            ColorSchemes.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
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
            themes.AddRange(Directory.GetFiles(directory, "*.xaml")
                .Where(path => !path.EndsWith("Base.xaml", StringComparison.OrdinalIgnoreCase))
                .Select(GetThemeDataFromPath));
        }

        return themes.OrderBy(theme => theme.Name).ToList();
    }

    private static List<FontTypefaceOption> GetTypefaceOptions(string familyName)
    {
        return
        [
            new FontTypefaceOption("Normal", "Normal", "Normal", "Normal"),
            new FontTypefaceOption("Bold", "Normal", "Bold", "Normal"),
            new FontTypefaceOption("Italic", "Italic", "Normal", "Normal"),
            new FontTypefaceOption("Bold Italic", "Italic", "Bold", "Normal")
        ];
    }

    private static FontTypefaceOption? FindTypefaceOption(IEnumerable<FontTypefaceOption> options, string? style, string? weight, string? stretch)
    {
        return options.FirstOrDefault(option =>
            string.Equals(option.Style, style, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.Weight, weight, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.Stretch, stretch, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
    }

    private static bool TryGetSetterValue<T>(WpfStyle style, System.Windows.DependencyProperty property, out T value)
    {
        var setter = style.Setters
            .OfType<WpfSetter>()
            .FirstOrDefault(currentSetter => currentSetter.Property == property);

        if (setter?.Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    private static string GetThemePath(string themeName)
    {
        var themeDirectories = new[]
        {
            Path.Combine(Constant.ProgramDirectory, Constant.Themes),
            Path.Combine(DataLocation.DataDirectory(), Constant.Themes)
        };

        foreach (var directory in themeDirectories)
        {
            var candidate = Path.Combine(directory, themeName + ".xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
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
