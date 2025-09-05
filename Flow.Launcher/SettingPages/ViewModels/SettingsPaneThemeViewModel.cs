using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.ViewModel;
using ModernWpf;
using ThemeManagerForColorSchemeSwitch = ModernWpf.ThemeManager;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneThemeViewModel : BaseModel
{
    public Settings Settings { get; }

    private readonly Theme _theme;

    private readonly string DefaultFont = Win32Helper.GetSystemDefaultFont();
    public string BackdropSubText => !Win32Helper.IsBackdropSupported() ? App.API.GetTranslation("BackdropTypeDisabledToolTip") : ""; 

    public static string LinkHowToCreateTheme => @"https://www.flowlauncher.com/theme-builder/";
    public static string LinkThemeGallery => "https://github.com/Flow-Launcher/Flow.Launcher/discussions/1438";

    private List<ThemeData> _themes;
    public List<ThemeData> Themes => _themes ??= App.API.GetAvailableThemes();

    private ThemeData _selectedTheme;
    public ThemeData SelectedTheme
    {
        get => _selectedTheme ??= Themes.Find(v => v == App.API.GetCurrentTheme());
        set
        {
            _selectedTheme = value;
            App.API.SetCurrentTheme(value);

            // Update UI state
            OnPropertyChanged(nameof(BackdropType));
            OnPropertyChanged(nameof(IsBackdropEnabled));
            OnPropertyChanged(nameof(IsDropShadowEnabled));
            OnPropertyChanged(nameof(DropShadowEffect));
        }
    }

    public bool IsBackdropEnabled
    {
        get
        {
            if (!Win32Helper.IsBackdropSupported()) return false;
            return SelectedTheme?.HasBlur ?? false;
        }
    }

    public bool IsDropShadowEnabled => !_theme.BlurEnabled;

    public bool DropShadowEffect
    {
        get => Settings.UseDropShadowEffect;
        set
        {
            if (_theme.BlurEnabled)
            {
                // Always DropShadowEffect = true with blur theme
                Settings.UseDropShadowEffect = true;
                return;
            }

            // User can change shadow with non-blur theme.
            if (value)
            {
                _theme.AddDropShadowEffectToCurrentTheme();
            }
            else
            {
                _theme.RemoveDropShadowEffectFromCurrentTheme();
            }

            Settings.UseDropShadowEffect = value;
            OnPropertyChanged(nameof(DropShadowEffect));
        }
    }

    public double WindowHeightSize
    {
        get => Settings.WindowHeightSize;
        set => Settings.WindowHeightSize = value;
    }

    public double ItemHeightSize
    {
        get => Settings.ItemHeightSize;
        set => Settings.ItemHeightSize = value;
    }

    public double QueryBoxFontSize
    {
        get => Settings.QueryBoxFontSize;
        set => Settings.QueryBoxFontSize = value;
    }

    public double ResultItemFontSize
    {
        get => Settings.ResultItemFontSize;
        set => Settings.ResultItemFontSize = value;
    }

    public double ResultSubItemFontSize
    {
        get => Settings.ResultSubItemFontSize;
        set => Settings.ResultSubItemFontSize = value;
    }

    public class ColorSchemeData : DropdownDataGeneric<ColorSchemes> { }

    public List<ColorSchemeData> ColorSchemes { get; } = DropdownDataGeneric<ColorSchemes>.GetValues<ColorSchemeData>("ColorScheme");
    public string ColorScheme
    {
        get => Settings.ColorScheme;
        set
        {
            ThemeManagerForColorSchemeSwitch.Current.ApplicationTheme = value switch
            {
                Constant.Light => ApplicationTheme.Light,
                Constant.Dark => ApplicationTheme.Dark,
                Constant.System => null,
                _ => ThemeManagerForColorSchemeSwitch.Current.ApplicationTheme
            };
            Settings.ColorScheme = value;
            _ = _theme.RefreshFrameAsync();
            Win32Helper.EnableWin32DarkMode(value);
        }
    }

    public List<string> TimeFormatList { get; } = new()
    {
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
    };

    public List<string> DateFormatList { get; } = new()
    {
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
    };

    public string TimeFormat
    {
        get => Settings.TimeFormat;
        set => Settings.TimeFormat = value;
    }

    public string DateFormat
    {
        get => Settings.DateFormat;
        set => Settings.DateFormat = value;
    }

    public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

    public bool KeepMaxResults
    {
        get => Settings.KeepMaxResults;
        set => Settings.KeepMaxResults = value;
    }

    public string ClockText => DateTime.Now.ToString(TimeFormat, CultureInfo.CurrentUICulture);

    public string DateText => DateTime.Now.ToString(DateFormat, CultureInfo.CurrentUICulture);

    public bool UseGlyphIcons
    {
        get => Settings.UseGlyphIcons;
        set => Settings.UseGlyphIcons = value;
    }

    public bool UseAnimation
    {
        get => Settings.UseAnimation;
        set => Settings.UseAnimation = value;
    }

    public class AnimationSpeedData : DropdownDataGeneric<AnimationSpeeds> { }
    public List<AnimationSpeedData> AnimationSpeeds { get; } = DropdownDataGeneric<AnimationSpeeds>.GetValues<AnimationSpeedData>("AnimationSpeed");

    public class BackdropTypeData : DropdownDataGeneric<BackdropTypes> { }

    public List<BackdropTypeData> BackdropTypesList { get; } =
        DropdownDataGeneric<BackdropTypes>.GetValues<BackdropTypeData>("BackdropTypes");
    
    public BackdropTypes BackdropType
    {
        get => Enum.IsDefined(typeof(BackdropTypes), Settings.BackdropType)
            ? Settings.BackdropType
            : BackdropTypes.None;
        set
        {
            if (!Enum.IsDefined(typeof(BackdropTypes), value))
            {
                value = BackdropTypes.None;
            }

            Settings.BackdropType = value;

            // Can only apply blur because drop shadow effect is not supported with backdrop
            // So drop shadow effect has been disabled
            _ = _theme.SetBlurForWindowAsync();

            OnPropertyChanged(nameof(IsDropShadowEnabled));
        }
    }

    public bool UseSound
    {
        get => Settings.UseSound;
        set => Settings.UseSound = value;
    }

    public bool ShowWMPWarning
    {
        get => !Settings.WMPInstalled && UseSound;
    }

    public bool EnableVolumeAdjustment
    {
        get => Settings.WMPInstalled;
    }

    public double SoundEffectVolume
    {
        get => Settings.SoundVolume;
        set => Settings.SoundVolume = value;
    }

    public bool ShowPlaceholder
    {
        get => Settings.ShowPlaceholder;
        set => Settings.ShowPlaceholder = value;
    }

    public string PlaceholderTextTip
    {
        get => string.Format(App.API.GetTranslation("PlaceholderTextTip"), App.API.GetTranslation("queryTextBoxPlaceholder"));
    }

    public string PlaceholderText
    {
        get => Settings.PlaceholderText;
        set => Settings.PlaceholderText = value;
    }

    public bool UseClock
    {
        get => Settings.UseClock;
        set => Settings.UseClock = value;
    }

    public bool UseDate
    {
        get => Settings.UseDate;
        set => Settings.UseDate = value;
    }

    public FontFamily ClockPanelFont { get; }

    public Brush PreviewBackground
    {
        get => WallpaperPathRetrieval.GetWallpaperBrush();
    }

    public ResultsViewModel PreviewResults { get; }

    public FontFamily SelectedQueryBoxFont
    {
        get
        {
            var fontExists = Fonts.SystemFontFamilies.Any(
                fontFamily =>
                    fontFamily.FamilyNames.Values != null &&
                    fontFamily.FamilyNames.Values.Contains(Settings.QueryBoxFont)
            );

            return fontExists switch
            {
                true => new FontFamily(Settings.QueryBoxFont),
                _ => new FontFamily(DefaultFont)
            };
        }
        set
        {
            Settings.QueryBoxFont = value.ToString();
            _theme.UpdateFonts();
        }
    }

    public FamilyTypeface SelectedQueryBoxFontFaces
    {
        get
        {
            var typeface = SyntaxSugars.CallOrRescueDefault(
                () => SelectedQueryBoxFont.ConvertFromInvariantStringsOrNormal(
                    Settings.QueryBoxFontStyle,
                    Settings.QueryBoxFontWeight,
                    Settings.QueryBoxFontStretch
                )
            );
            return typeface;
        }
        set
        {
            Settings.QueryBoxFontStretch = value.Stretch.ToString();
            Settings.QueryBoxFontWeight = value.Weight.ToString();
            Settings.QueryBoxFontStyle = value.Style.ToString();
            _theme.UpdateFonts();
        }
    }

    public FontFamily SelectedResultFont
    {
        get
        {
            var fontExists = Fonts.SystemFontFamilies.Any(
                fontFamily =>
                    fontFamily.FamilyNames.Values != null &&
                    fontFamily.FamilyNames.Values.Contains(Settings.ResultFont)
            );
            return fontExists switch
            {
                true => new FontFamily(Settings.ResultFont),
                _ => new FontFamily(DefaultFont)
            };
        }
        set
        {
            Settings.ResultFont = value.ToString();
            _theme.UpdateFonts();
        }
    }

    public FamilyTypeface SelectedResultFontFaces
    {
        get
        {
            var typeface = SyntaxSugars.CallOrRescueDefault(
                () => SelectedResultFont.ConvertFromInvariantStringsOrNormal(
                    Settings.ResultFontStyle,
                    Settings.ResultFontWeight,
                    Settings.ResultFontStretch
                )
            );
            return typeface;
        }
        set
        {
            Settings.ResultFontStretch = value.Stretch.ToString();
            Settings.ResultFontWeight = value.Weight.ToString();
            Settings.ResultFontStyle = value.Style.ToString();
            _theme.UpdateFonts();
        }
    }

    public FontFamily SelectedResultSubFont
    {
        get
        {
            if (Fonts.SystemFontFamilies.Any(o =>
                    o.FamilyNames.Values != null &&
                    o.FamilyNames.Values.Contains(Settings.ResultSubFont)))
            {
                var font = new FontFamily(Settings.ResultSubFont);
                return font;
            }
            else
            {
                var font = new FontFamily(DefaultFont);
                return font;
            }
        }
        set
        {
            Settings.ResultSubFont = value.ToString();
            _theme.UpdateFonts();
        }
    }

    public FamilyTypeface SelectedResultSubFontFaces
    {
        get
        {
            var typeface = SyntaxSugars.CallOrRescueDefault(
                () => SelectedResultSubFont.ConvertFromInvariantStringsOrNormal(
                    Settings.ResultSubFontStyle,
                    Settings.ResultSubFontWeight,
                    Settings.ResultSubFontStretch
                ));
            return typeface;
        }
        set
        {
            Settings.ResultSubFontStretch = value.Stretch.ToString();
            Settings.ResultSubFontWeight = value.Weight.ToString();
            Settings.ResultSubFontStyle = value.Style.ToString();
            _theme.UpdateFonts();
        }
    }

    public string ThemeImage => Constant.QueryTextBoxIconImagePath;

    public SettingsPaneThemeViewModel(Settings settings, Theme theme)
    {
        Settings = settings;
        _theme = theme;
        ClockPanelFont = new FontFamily(DefaultFont);
        var results = new List<Result>
            {
                new()
                {
                    Title = App.API.GetTranslation("SampleTitleExplorer"),
                    SubTitle = App.API.GetTranslation("SampleSubTitleExplorer"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.Explorer\Images\explorer.png"
                    )
                },
                new()
                {
                    Title = App.API.GetTranslation("SampleTitleWebSearch"),
                    SubTitle = App.API.GetTranslation("SampleSubTitleWebSearch"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.WebSearch\Images\web_search.png"
                    )
                },
                new()
                {
                    Title = App.API.GetTranslation("SampleTitleProgram"),
                    SubTitle = App.API.GetTranslation("SampleSubTitleProgram"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.Program\Images\program.png"
                    )
                },
                new()
                {
                    Title = App.API.GetTranslation("SampleTitleProcessKiller"),
                    SubTitle = App.API.GetTranslation("SampleSubTitleProcessKiller"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.ProcessKiller\Images\app.png"
                    )
                }
            };
        // Set main view model to null because the results are for preview only
        var vm = new ResultsViewModel(Settings, null);
        vm.AddResults(results, "PREVIEW");
        PreviewResults = vm;
    }

    [RelayCommand]
    private void OpenThemesFolder()
    {
        App.API.OpenDirectory(DataLocation.ThemesDirectory);
    }

    [RelayCommand]
    public void Reset()
    {
        SelectedQueryBoxFont = new FontFamily(DefaultFont);
        SelectedQueryBoxFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        QueryBoxFontSize = 16;

        SelectedResultFont = new FontFamily(DefaultFont);
        SelectedResultFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        ResultItemFontSize = 16;

        SelectedResultSubFont = new FontFamily(DefaultFont);
        SelectedResultSubFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        ResultSubItemFontSize = 13;

        WindowHeightSize = 42;
        ItemHeightSize = 58;
    }
    
    [RelayCommand]
    private void Import()
    {
        var resourceDictionary = _theme.GetCurrentResourceDictionary();
        
        if (resourceDictionary["QueryBoxStyle"] is Style queryBoxStyle)
        {
            var fontSizeSetter = queryBoxStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == TextBox.FontSizeProperty);
            if (fontSizeSetter?.Value is double fontSize)
            {
                QueryBoxFontSize = fontSize;
            }
            
            var heightSetter = queryBoxStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == FrameworkElement.HeightProperty);
            if (heightSetter?.Value is double height)
            {
                WindowHeightSize = height;
            }
        }
        
        if (resourceDictionary["ResultItemHeight"] is double resultItemHeight)
        {
            ItemHeightSize = resultItemHeight;
        }
        
        if (resourceDictionary["ItemTitleStyle"] is Style itemTitleStyle)
        {
            var fontSizeSetter = itemTitleStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == TextBlock.FontSizeProperty);
            if (fontSizeSetter?.Value is double fontSize)
            {
                ResultItemFontSize = fontSize;
            }
        }
        
        if (resourceDictionary["ItemSubTitleStyle"] is Style itemSubTitleStyle)
        {
            var fontSizeSetter = itemSubTitleStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == TextBlock.FontSizeProperty);
            if (fontSizeSetter?.Value is double fontSize)
            {
                ResultSubItemFontSize = fontSize;
            }
        }
    }
}
