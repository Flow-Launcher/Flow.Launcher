using System;
using System.Collections.Generic;
using System.Windows;
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
using Flow.Launcher.ViewModel;
using ModernWpf;
using ThemeManager = Flow.Launcher.Core.Resource.ThemeManager;
using ThemeManagerForColorSchemeSwitch = ModernWpf.ThemeManager;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneThemeViewModel : BaseModel
{
    private const string DefaultFont = "Segoe UI";
    public Settings Settings { get; }

    public static string LinkHowToCreateTheme => @"https://flowlauncher.com/docs/#/how-to-create-a-theme";
    public static string LinkThemeGallery => "https://github.com/Flow-Launcher/Flow.Launcher/discussions/1438";

    private Theme.ThemeData _selectedTheme;
    public Theme.ThemeData SelectedTheme
    {
        get => _selectedTheme ??= Themes.Find(v => v.FileNameWithoutExtension == Settings.Theme);
        set
        {
            _selectedTheme = value;
            ThemeManager.Instance.ChangeTheme(value.FileNameWithoutExtension);

            if (ThemeManager.Instance.BlurEnabled && Settings.UseDropShadowEffect == false)
            {
                DropShadowEffect = true;
                OnPropertyChanged(nameof(IsDropShadowEnabled));
            }

            ThemeManager.Instance.RefreshFrame();
            //ThemeManager.Instance.SetBlurForWindow();
        }
    }
    public bool IsBackdropEnabled => SelectedTheme?.HasBlur ?? false;
    public bool IsDropShadowEnabled => !ThemeManager.Instance.BlurEnabled;

    public bool DropShadowEffect
    {
        get => Settings.UseDropShadowEffect;
        set
        {
            if (ThemeManager.Instance.BlurEnabled && value == false)
            {
                Settings.UseDropShadowEffect = true;
                return;
            }

            if (value)
            {
                ThemeManager.Instance.AddDropShadowEffectToCurrentTheme();
            }
            else
            {
                ThemeManager.Instance.RemoveDropShadowEffectFromCurrentTheme();
            }

            Settings.UseDropShadowEffect = value;
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

    private List<Theme.ThemeData> _themes;
    public List<Theme.ThemeData> Themes => _themes ??= ThemeManager.Instance.LoadAvailableThemes();

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

            ThemeManager.Instance.RefreshFrame();

            Settings.ColorScheme = value;
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
        "dd.MM.yyyy"
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

    public class BackdropTypeData : DropdownDataGeneric<BackdropTypes>
    {
        public void ApplyBackdrop()
        {
            //IntPtr hWnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            //if (hWnd == IntPtr.Zero)
            //    return;

            //int backdropValue = Value switch
            //{
            //    BackdropTypes.Acrylic => 3, // ✅ Acrylic (DWM_SYSTEMBACKDROP_TYPE = 3)
            //    BackdropTypes.Mica => 2,    // ✅ Mica (DWM_SYSTEMBACKDROP_TYPE = 2)
            //    BackdropTypes.MicaAlt => 4, // ✅ MicaAlt (DWM_SYSTEMBACKDROP_TYPE = 4)
            //    _ => 0                      // ✅ None (DWM_SYSTEMBACKDROP_TYPE = 0)
            //};
            ThemeManager.Instance.SetBlurForWindow();
            //Methods.SetWindowAttribute(hWnd, ParameterTypes.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdropValue);
        }
    }

    // ✅ BackdropTypeData 리스트 (Dropdown과 연동)
    public List<BackdropTypeData> BackdropTypesList { get; } =
        DropdownDataGeneric<BackdropTypes>.GetValues<BackdropTypeData>("BackdropTypes");

    // ✅ BackdropType 속성 (값 저장 + 자동 적용)
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

            // ✅ BackdropTypeData 리스트에서 해당하는 값 찾기
            var backdropData = BackdropTypesList.FirstOrDefault(b => b.Value == value);
            backdropData?.ApplyBackdrop();

            // ✅ DropShadow 활성화 여부 갱신
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

    public Brush PreviewBackground
    {
        get => WallpaperPathRetrieval.GetWallpaperBrush();
    }

    public ResultsViewModel PreviewResults
    {
        get
        {
            var results = new List<Result>
            {
                new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("SampleTitleExplorer"),
                    SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleExplorer"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.Explorer\Images\explorer.png"
                    )
                },
                new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("SampleTitleWebSearch"),
                    SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleWebSearch"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.WebSearch\Images\web_search.png"
                    )
                },
                new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("SampleTitleProgram"),
                    SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleProgram"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.Program\Images\program.png"
                    )
                },
                new Result
                {
                    Title = InternationalizationManager.Instance.GetTranslation("SampleTitleProcessKiller"),
                    SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleProcessKiller"),
                    IcoPath = Path.Combine(
                        Constant.ProgramDirectory,
                        @"Plugins\Flow.Launcher.Plugin.ProcessKiller\Images\app.png"
                    )
                }
            };
            var vm = new ResultsViewModel(Settings);
            vm.AddResults(results, "PREVIEW");
            return vm;
        }
    }

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
            ThemeManager.Instance.ChangeTheme(Settings.Theme);
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
            ThemeManager.Instance.ChangeTheme(Settings.Theme);
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
            ThemeManager.Instance.ChangeTheme(Settings.Theme);
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
            ThemeManager.Instance.ChangeTheme(Settings.Theme);
        }
    }

    public FontFamily SelectedResultSubFont
    {
        get
        {
            if (Fonts.SystemFontFamilies.Count(o =>
                    o.FamilyNames.Values != null &&
                    o.FamilyNames.Values.Contains(Settings.ResultSubFont)) > 0)
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
            ThemeManager.Instance.ChangeTheme(Settings.Theme);
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
            ThemeManager.Instance.ChangeTheme(Settings.Theme);
        }
    }

    public string ThemeImage => Constant.QueryTextBoxIconImagePath;

    public SettingsPaneThemeViewModel(Settings settings)
    {
        Settings = settings;
    }

    [RelayCommand]
    private void OpenThemesFolder()
    {
        App.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Themes));
    }

    [RelayCommand]
    public void Reset()
    {
        SelectedQueryBoxFont = new FontFamily(DefaultFont);
        SelectedQueryBoxFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        QueryBoxFontSize = 20;

        SelectedResultFont = new FontFamily(DefaultFont);
        SelectedResultFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        ResultItemFontSize = 16;

        SelectedResultSubFont = new FontFamily(DefaultFont);
        SelectedResultSubFontFaces = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };
        ResultSubItemFontSize = 13;

        WindowHeightSize = 42;
        ItemHeightSize = 58;
    }
}
