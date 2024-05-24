﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private CultureInfo Culture => CultureInfo.DefaultThreadCurrentCulture;

    public Settings Settings { get; }

    public static string LinkHowToCreateTheme => @"https://flowlauncher.com/docs/#/how-to-create-a-theme";
    public static string LinkThemeGallery => "https://github.com/Flow-Launcher/Flow.Launcher/discussions/1438";

    public string SelectedTheme
    {
        get => Settings.Theme;
        set
        {
            ThemeManager.Instance.ChangeTheme(value);

            if (ThemeManager.Instance.BlurEnabled && Settings.UseDropShadowEffect)
                DropShadowEffect = false;
        }
    }

    public bool DropShadowEffect
    {
        get => Settings.UseDropShadowEffect;
        set
        {
            if (ThemeManager.Instance.BlurEnabled && value)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("shadowEffectNotAllowed"));
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

    public List<string> Themes =>
        ThemeManager.Instance.LoadAvailableThemes().Select(Path.GetFileNameWithoutExtension).ToList();


    public class ColorScheme
    {
        public string Display { get; set; }
        public ColorSchemes Value { get; set; }
    }

    public List<ColorScheme> ColorSchemes
    {
        get
        {
            List<ColorScheme> modes = new List<ColorScheme>();
            var enums = (ColorSchemes[])Enum.GetValues(typeof(ColorSchemes));
            foreach (var e in enums)
            {
                var key = $"ColorScheme{e}";
                var display = InternationalizationManager.Instance.GetTranslation(key);
                var m = new ColorScheme { Display = display, Value = e, };
                modes.Add(m);
            }

            return modes;
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
        "dd', 'MMMM"
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

    public string ClockText => DateTime.Now.ToString(TimeFormat, Culture);

    public string DateText => DateTime.Now.ToString(DateFormat, Culture);

    public double WindowWidthSize
    {
        get => Settings.WindowSize;
        set => Settings.WindowSize = value;
    }

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

    public class AnimationSpeed
    {
        public string Display { get; set; }
        public AnimationSpeeds Value { get; set; }
    }

    public List<AnimationSpeed> AnimationSpeeds
    {
        get
        {
            List<AnimationSpeed> speeds = new List<AnimationSpeed>();
            var enums = (AnimationSpeeds[])Enum.GetValues(typeof(AnimationSpeeds));
            foreach (var e in enums)
            {
                var key = $"AnimationSpeed{e}";
                var display = InternationalizationManager.Instance.GetTranslation(key);
                var m = new AnimationSpeed { Display = display, Value = e, };
                speeds.Add(m);
            }

            return speeds;
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
        get
        {
            var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
            if (wallpaper is not null && File.Exists(wallpaper))
            {
                var memStream = new MemoryStream(File.ReadAllBytes(wallpaper));
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = memStream;
                bitmap.DecodePixelWidth = 800;
                bitmap.DecodePixelHeight = 600;
                bitmap.EndInit();
                return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            }

            var wallpaperColor = WallpaperPathRetrieval.GetWallpaperColor();
            return new SolidColorBrush(wallpaperColor);
        }
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
                _ => new FontFamily("Segoe UI")
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
                _ => new FontFamily("Segoe UI")
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

    public string ThemeImage => Constant.QueryTextBoxIconImagePath;

    [RelayCommand]
    private void OpenThemesFolder()
    {
        App.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Themes));
    }

    public void UpdateColorScheme()
    {
        ThemeManagerForColorSchemeSwitch.Current.ApplicationTheme = Settings.ColorScheme switch
        {
            Constant.Light => ApplicationTheme.Light,
            Constant.Dark => ApplicationTheme.Dark,
            Constant.System => null,
            _ => ThemeManagerForColorSchemeSwitch.Current.ApplicationTheme
        };
    }

    public SettingsPaneThemeViewModel(Settings settings)
    {
        Settings = settings;
    }
}
