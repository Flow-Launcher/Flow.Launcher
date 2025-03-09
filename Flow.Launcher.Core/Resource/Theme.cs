using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Shell;
using static Flow.Launcher.Core.Resource.Theme.ParameterTypes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Diagnostics;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TextBox = System.Windows.Controls.TextBox;

namespace Flow.Launcher.Core.Resource
{
    public class Theme
    {
        private const string ThemeMetadataNamePrefix = "Name:";
        private const string ThemeMetadataIsDarkPrefix = "IsDark:";
        private const string ThemeMetadataHasBlurPrefix = "HasBlur:";

        private const int ShadowExtraMargin = 32;

        private readonly IPublicAPI _api;
        private readonly Settings _settings;
        private readonly List<string> _themeDirectories = new();
        private ResourceDictionary _oldResource;
        private string _oldTheme;
        private const string Folder = Constant.Themes;
        private const string Extension = ".xaml";
        private string DirectoryPath => Path.Combine(Constant.ProgramDirectory, Folder);
        private string UserDirectoryPath => Path.Combine(DataLocation.DataDirectory(), Folder);

        public bool BlurEnabled { get; set; }

        private double mainWindowWidth;

        public Theme(IPublicAPI publicAPI, Settings settings)
        {
            _api = publicAPI;
            _settings = settings;

            _themeDirectories.Add(DirectoryPath);
            _themeDirectories.Add(UserDirectoryPath);
            MakeSureThemeDirectoriesExist();

            var dicts = Application.Current.Resources.MergedDictionaries;
            _oldResource = dicts.First(d =>
            {
                if (d.Source == null)
                    return false;

                var p = d.Source.AbsolutePath;
                var dir = Path.GetDirectoryName(p).NonNull();
                var info = new DirectoryInfo(dir);
                var f = info.Name;
                var e = Path.GetExtension(p);
                var found = f == Folder && e == Extension;
                return found;
            });
            _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
        }

        #region Blur Handling
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            Default = 0,
            DoNotRound = 1,
            Round = 2,
            RoundSmall = 3
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute, int cbAttribute);
        public static void SetWindowCornerPreference(System.Windows.Window window, DWM_WINDOW_CORNER_PREFERENCE preference)
        {
            IntPtr hWnd = new WindowInteropHelper(window).Handle;
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        public class ParameterTypes
        {

            [Flags]
            public enum DWMWINDOWATTRIBUTE
            {
                DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
                DWMWA_SYSTEMBACKDROP_TYPE = 38,
                DWMWA_TRANSITIONS_FORCEDISABLED = 3,
                DWMWA_BORDER_COLOR
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;      // width of left border that retains its size
                public int cxRightWidth;     // width of right border that retains its size
                public int cyTopHeight;      // height of top border that retains its size
                public int cyBottomHeight;   // height of bottom border that retains its size
            };
        }

        public static class Methods
        {
            [DllImport("DwmApi.dll")]
            static extern int DwmExtendFrameIntoClientArea(
                IntPtr hwnd,
                ref ParameterTypes.MARGINS pMarInset);

            [DllImport("dwmapi.dll")]
            static extern int DwmSetWindowAttribute(IntPtr hwnd, ParameterTypes.DWMWINDOWATTRIBUTE dwAttribute, ref int pvAttribute, int cbAttribute);

            public static int ExtendFrame(IntPtr hwnd, ParameterTypes.MARGINS margins)
                => DwmExtendFrameIntoClientArea(hwnd, ref margins);

            public static int SetWindowAttribute(IntPtr hwnd, ParameterTypes.DWMWINDOWATTRIBUTE attribute, int parameter)
                => DwmSetWindowAttribute(hwnd, attribute, ref parameter, Marshal.SizeOf<int>());
        }

        System.Windows.Window mainWindow = Application.Current.MainWindow;

        public void RefreshFrame()
        {
            IntPtr mainWindowPtr = new WindowInteropHelper(mainWindow).Handle;
            if (mainWindowPtr == IntPtr.Zero)
                return;

            HwndSource mainWindowSrc = HwndSource.FromHwnd(mainWindowPtr);
            if (mainWindowSrc == null)
                return;

            ParameterTypes.MARGINS margins = new ParameterTypes.MARGINS();
            margins.cxLeftWidth = -1;
            margins.cxRightWidth = -1;
            margins.cyTopHeight = -1;
            margins.cyBottomHeight = -1;
            Methods.ExtendFrame(mainWindowSrc.Handle, margins);

            // Remove OS minimizing/maximizing animation
            // Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, 3);

            // Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, 0x00FF0000);
            // Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, 3);

            // The timing of adding the shadow effect should vary depending on whether the theme is transparent.
            if (BlurEnabled)
            {
                AutoDropShadow();
            }
            SetBlurForWindow();

            if (!BlurEnabled)
            {
                AutoDropShadow();
            }
        }


        public void AutoDropShadow()
        {
            if (_settings.UseDropShadowEffect)
            {
                RemoveDropShadowEffectFromCurrentTheme();
                if (BlurEnabled)
                {
                    SetWindowCornerPreference("Round");
                }
                else
                {
                    SetWindowCornerPreference("Default");
                    AddDropShadowEffectToCurrentTheme();
                }
            }
            else
            {
                RemoveDropShadowEffectFromCurrentTheme();
                if (BlurEnabled)
                {
                    SetWindowCornerPreference("Default");
                }
                else
                {
                    RemoveDropShadowEffectFromCurrentTheme();
                }
            }
        }

        public void SetWindowCornerPreference(string cornerType)
        {
            DWM_WINDOW_CORNER_PREFERENCE preference = cornerType switch
            {
                "DoNotRound" => DWM_WINDOW_CORNER_PREFERENCE.DoNotRound,
                "Round" => DWM_WINDOW_CORNER_PREFERENCE.Round,
                "RoundSmall" => DWM_WINDOW_CORNER_PREFERENCE.RoundSmall,
                "Default" => DWM_WINDOW_CORNER_PREFERENCE.Default,
                _ => DWM_WINDOW_CORNER_PREFERENCE.Default,
            };

            SetWindowCornerPreference(mainWindow, preference);
        }

        public void SetCornerForWindow()
        {
            var dict = GetThemeResourceDictionary(_settings.Theme);
            if (dict == null)
                return;
            if (dict.Contains("CornerType") && dict["CornerType"] is string cornerMode)
            {
                DWM_WINDOW_CORNER_PREFERENCE preference = cornerMode switch
                {
                    "DoNotRound" => DWM_WINDOW_CORNER_PREFERENCE.DoNotRound,
                    "Round" => DWM_WINDOW_CORNER_PREFERENCE.Round,
                    "RoundSmall" => DWM_WINDOW_CORNER_PREFERENCE.RoundSmall,
                    _ => DWM_WINDOW_CORNER_PREFERENCE.Default,
                };

                SetWindowCornerPreference(mainWindow, preference);
           
            }
            else
            {
                SetWindowCornerPreference(mainWindow, DWM_WINDOW_CORNER_PREFERENCE.Default);
 
            }
        }

        /// <summary>
        /// Sets the blur for a window via SetWindowCompositionAttribute
        /// </summary>
        public void SetBlurForWindow()
        {
            var dict = GetThemeResourceDictionary(_settings.Theme);
            if (dict == null)
                return;

            var windowBorderStyle = dict["WindowBorderStyle"] as Style;
            if (windowBorderStyle == null)
                return;

            // ✅ 설정된 BackdropType 확인
            int backdropValue = _settings.BackdropType switch
            {
                BackdropTypes.Acrylic => 2, // Acrylic (DWM_SYSTEMBACKDROP_TYPE = 2)
                BackdropTypes.Mica => 3,    // Mica (DWM_SYSTEMBACKDROP_TYPE = 3)
                BackdropTypes.MicaAlt => 4, // MicaAlt (DWM_SYSTEMBACKDROP_TYPE = 4)
                _ => 0                      // None (DWM_SYSTEMBACKDROP_TYPE = 0)
            };

            Debug.WriteLine("~~~~~~~~~~~~~~~~~~~~");
            Debug.WriteLine($"Backdrop Mode: {BlurMode()}, DWM Value: {backdropValue}");

            Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdropValue);

            if (BlurEnabled)
            {
                // ✅ Mica 또는 MicaAlt인 경우 배경을 투명하게 설정
                if (_settings.BackdropType == BackdropTypes.Mica || _settings.BackdropType == BackdropTypes.MicaAlt)
                {
                    windowBorderStyle.Setters.Remove(windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property.Name == "Background"));
                    windowBorderStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)))); // 드래그 가능 투명색
                }
                else
                {
                    windowBorderStyle.Setters.Remove(windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property.Name == "Background"));
                    windowBorderStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
                }

                ThemeModeColor(BlurMode()); // ✅ 테마 모드 적용
            }
            else
            {
                // ✅ Blur가 비활성화되면 기본 스타일 적용
                Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, 0);
            }

            UpdateResourceDictionary(dict);
        }


        // Get Background Color from WindowBorderStyle when there not color for BG.
        private Color GetWindowBorderStyleBackground()
        {
            var Resources = GetThemeResourceDictionary(_settings.Theme);
            var windowBorderStyle = (Style)Resources["WindowBorderStyle"];

            var backgroundSetter = windowBorderStyle.Setters
                .OfType<Setter>()
                .FirstOrDefault(s => s.Property == Border.BackgroundProperty);

            if (backgroundSetter != null)
            {
                // Background's Value is DynamicColor Case
                var backgroundValue = backgroundSetter.Value;

                if (backgroundValue is SolidColorBrush solidColorBrush)
                {
                    return solidColorBrush.Color; // Return SolidColorBrush's Color
                }
                else if (backgroundValue is DynamicResourceExtension dynamicResource)
                {
                    // When DynamicResource Extension it is, Key is resource's name.
                    var resourceKey = backgroundSetter.Value.ToString();

                    // find key in resource and return color.
                    if (Resources.Contains(resourceKey))
                    {
                        var colorResource = Resources[resourceKey];
                        if (colorResource is SolidColorBrush colorBrush)
                        {
                            return colorBrush.Color;
                        }
                        else if (colorResource is Color color)
                        {
                            return color;
                        }
                    }
                }
            }

            return Colors.Transparent; // Default is transparent
        }

        public void ThemeModeColor(string Mode)
        {
            var dict = GetThemeResourceDictionary(_settings.Theme);

            Color lightBG;
            Color darkBG;

            // get lightBG value. if not, get windowborderstyle's background.
            try
            {
                lightBG = dict.Contains("lightBG") ? (Color)dict["lightBG"] : GetWindowBorderStyleBackground();
            }
            catch (Exception)
            {
                // if not lightBG, use windowborderstyle's background.
                lightBG = GetWindowBorderStyleBackground();
            }

            // get darkBG value, (if not, use lightBG)
            try
            {
                darkBG = dict.Contains("darkBG") ? (Color)dict["darkBG"] : lightBG;
            }
            catch (Exception)
            {
                darkBG = lightBG; // if not darkBG, use lightBG
            }

            // ✅ 백드롭 타입 확인 (Mica 또는 MicaAlt인 경우 배경을 투명하게 설정)
            bool isMica = _settings.BackdropType == BackdropTypes.Mica || _settings.BackdropType == BackdropTypes.MicaAlt;

            if (Mode == "Auto")
            {
                int themeValue = (int)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                string colorScheme = _settings.ColorScheme;
                bool isDarkMode = themeValue == 0; // 0 is dark mode.

                if (colorScheme == "System")
                {
                    if (isDarkMode)
                    {
                        if (isMica)
                        {
                            mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                        }
                        else
                        {
                            mainWindow.Background = new SolidColorBrush(darkBG);
                        }

                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
                        return;
                    }
                    else
                    {
                        if (isMica)
                        {
                            mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                        }
                        else
                        {
                            mainWindow.Background = new SolidColorBrush(lightBG);
                        }

                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 0);
                        return;
                    }
                }
                else
                {
                    if (colorScheme == "Dark")
                    {
                        if (isMica)
                        {
                            mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                        }
                        else
                        {
                            mainWindow.Background = new SolidColorBrush(darkBG);
                        }

                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
                        return;
                    }
                    else if (colorScheme == "Light")
                    {
                        if (isMica)
                        {
                            mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                        }
                        else
                        {
                            mainWindow.Background = new SolidColorBrush(lightBG);
                        }

                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 0);
                        return;
                    }
                }
            }
            else if (Mode == "Dark")
            {
                if (isMica)
                {
                    mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                }
                else
                {
                    mainWindow.Background = new SolidColorBrush(darkBG);
                }

                Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
                return;
            }
            else if (Mode == "Light")
            {
                if (isMica)
                {
                    mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                }
                else
                {
                    mainWindow.Background = new SolidColorBrush(lightBG);
                }

                Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 0);
                return;
            }
            else
            {
                if (isMica)
                {
                    mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // ✅ Mica 배경 투명 처리
                }
                else
                {
                    mainWindow.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
        }



        public bool IsBlurTheme()
        {
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                var resource = Application.Current.TryFindResource("ThemeBlurEnabled");

                if (resource is bool)
                    return (bool)resource;

                return false;
            }

            return false;
        }
        public string BlurMode()
        {
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                var resource = Application.Current.TryFindResource("BlurMode");

                if (resource is string)
                    return (string)resource;

                return null;
            }

            return null;
        }

        #endregion

        private void MakeSureThemeDirectoriesExist()
        {
            foreach (var dir in _themeDirectories.Where(dir => !Directory.Exists(dir)))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception e)
                {
                    Log.Exception($"|Theme.MakesureThemeDirectoriesExist|Exception when create directory <{dir}>", e);
                }
            }
        }

        public bool ChangeTheme(string theme)
        {
            const string defaultTheme = Constant.DefaultTheme;

            string path = GetThemePath(theme);
            try
            {
                if (string.IsNullOrEmpty(path))
                    throw new DirectoryNotFoundException("Theme path can't be found <{path}>");

                // reload all resources even if the theme itself hasn't changed in order to pickup changes
                // to things like fonts
                UpdateResourceDictionary(GetResourceDictionary(theme));

                _settings.Theme = theme;


                //always allow re-loading default theme, in case of failure of switching to a new theme from default theme
                if (_oldTheme != theme || theme == defaultTheme)
                {
                    _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
                }

                BlurEnabled = Win32Helper.IsBlurTheme();
                if (_settings.UseDropShadowEffect)
                    AddDropShadowEffectToCurrentTheme();



                //Win32Helper.SetBlurForWindow(Application.Current.MainWindow, BlurEnabled);
                SetBlurForWindow();
            }
            catch (DirectoryNotFoundException)
            {
                Log.Error($"|Theme.ChangeTheme|Theme <{theme}> path can't be found");
                if (theme != defaultTheme)
                {
                    _api.ShowMsgBox(string.Format(InternationalizationManager.Instance.GetTranslation("theme_load_failure_path_not_exists"), theme));
                    ChangeTheme(defaultTheme);
                }
                return false;
            }
            catch (XamlParseException)
            {
                Log.Error($"|Theme.ChangeTheme|Theme <{theme}> fail to parse");
                if (theme != defaultTheme)
                {
                    _api.ShowMsgBox(string.Format(InternationalizationManager.Instance.GetTranslation("theme_load_failure_parse_error"), theme));
                    ChangeTheme(defaultTheme);
                }
                return false;
            }
            return true;
        }

        private void UpdateResourceDictionary(ResourceDictionary dictionaryToUpdate)
        {
            var dicts = Application.Current.Resources.MergedDictionaries;

            dicts.Remove(_oldResource);
            dicts.Add(dictionaryToUpdate);
            _oldResource = dictionaryToUpdate;
        }

        private ResourceDictionary GetThemeResourceDictionary(string theme)
        {
            var uri = GetThemePath(theme);
            var dict = new ResourceDictionary
            {
                Source = new Uri(uri, UriKind.Absolute)
            };

            return dict;
        }

        private ResourceDictionary CurrentThemeResourceDictionary() => GetThemeResourceDictionary(_settings.Theme);

        public ResourceDictionary GetResourceDictionary(string theme)
        {
            var dict = GetThemeResourceDictionary(theme);

            if (dict["QueryBoxStyle"] is Style queryBoxStyle &&
                dict["QuerySuggestionBoxStyle"] is Style querySuggestionBoxStyle)
            {
                var fontFamily = new FontFamily(_settings.QueryBoxFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.QueryBoxFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.QueryBoxFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.QueryBoxFontStretch);

                queryBoxStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, fontFamily));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontStyleProperty, fontStyle));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontWeightProperty, fontWeight));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontStretchProperty, fontStretch));

                var caretBrushPropertyValue = queryBoxStyle.Setters.OfType<Setter>().Any(x => x.Property.Name == "CaretBrush");
                var foregroundPropertyValue = queryBoxStyle.Setters.OfType<Setter>().Where(x => x.Property.Name == "Foreground")
                    .Select(x => x.Value).FirstOrDefault();
                if (!caretBrushPropertyValue && foregroundPropertyValue != null) //otherwise BaseQueryBoxStyle will handle styling
                    queryBoxStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, foregroundPropertyValue));

                // Query suggestion box's font style is aligned with query box
                querySuggestionBoxStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, fontFamily));
                querySuggestionBoxStyle.Setters.Add(new Setter(TextBox.FontStyleProperty, fontStyle));
                querySuggestionBoxStyle.Setters.Add(new Setter(TextBox.FontWeightProperty, fontWeight));
                querySuggestionBoxStyle.Setters.Add(new Setter(TextBox.FontStretchProperty, fontStretch));
            }

            if (dict["ItemTitleStyle"] is Style resultItemStyle &&
                dict["ItemTitleSelectedStyle"] is Style resultItemSelectedStyle &&
                dict["ItemHotkeyStyle"] is Style resultHotkeyItemStyle &&
                dict["ItemHotkeySelectedStyle"] is Style resultHotkeyItemSelectedStyle)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(_settings.ResultFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultFontStretch));

                Setter[] setters = { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(
                    new[] { resultItemStyle, resultItemSelectedStyle, resultHotkeyItemStyle, resultHotkeyItemSelectedStyle }, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            if (
                dict["ItemSubTitleStyle"] is Style resultSubItemStyle &&
                dict["ItemSubTitleSelectedStyle"] is Style resultSubItemSelectedStyle)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(_settings.ResultSubFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultSubFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultSubFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultSubFontStretch));

                Setter[] setters = { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(
                    new[] {  resultSubItemStyle,resultSubItemSelectedStyle}, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            /* Ignore Theme Window Width and use setting */
            var windowStyle = dict["WindowStyle"] as Style;
            var width = _settings.WindowSize;
            windowStyle.Setters.Add(new Setter(System.Windows.Window.WidthProperty, width));
            mainWindowWidth = (double)width;
            return dict;
        }

        private ResourceDictionary GetCurrentResourceDictionary( )
        {
            return  GetResourceDictionary(_settings.Theme);
        }

        public List<ThemeData> LoadAvailableThemes()
        {
            List<ThemeData> themes = new List<ThemeData>();
            foreach (var themeDirectory in _themeDirectories)
            {
                var filePaths = Directory
                    .GetFiles(themeDirectory)
                    .Where(filePath => filePath.EndsWith(Extension) && !filePath.EndsWith("Base.xaml"))
                    .Select(GetThemeDataFromPath);
                themes.AddRange(filePaths);
            }

            return themes.OrderBy(o => o.Name).ToList();
        }

        private ThemeData GetThemeDataFromPath(string path)
        {
            using var reader = XmlReader.Create(path);
            reader.Read();

            var extensionlessName = Path.GetFileNameWithoutExtension(path);

            if (reader.NodeType is not XmlNodeType.Comment)
                return new ThemeData(extensionlessName, extensionlessName);

            var commentLines = reader.Value.Trim().Split('\n').Select(v => v.Trim());

            var name = extensionlessName;
            bool? isDark = null;
            bool? hasBlur = null;
            foreach (var line in commentLines)
            {
                if (line.StartsWith(ThemeMetadataNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = line.Remove(0, ThemeMetadataNamePrefix.Length).Trim();
                }
                else if (line.StartsWith(ThemeMetadataIsDarkPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    isDark = bool.Parse(line.Remove(0, ThemeMetadataIsDarkPrefix.Length).Trim());
                }
                else if (line.StartsWith(ThemeMetadataHasBlurPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    hasBlur = bool.Parse(line.Remove(0, ThemeMetadataHasBlurPrefix.Length).Trim());
                }
            }

            return new ThemeData(extensionlessName, name, isDark, hasBlur);
        }

        private string GetThemePath(string themeName)
        {
            foreach (string themeDirectory in _themeDirectories)
            {
                string path = Path.Combine(themeDirectory, themeName + Extension);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        public void AddDropShadowEffectToCurrentTheme()
        {

            //SetWindowCornerPreference("Default");
            var dict = GetCurrentResourceDictionary();

                var windowBorderStyle = dict["WindowBorderStyle"] as Style;

                var effectSetter = new Setter
                {
                    Property = Border.EffectProperty,
                    Value = new DropShadowEffect
                    {
                        Opacity = 0.3,
                        ShadowDepth = 12,
                        Direction = 270,
                        BlurRadius = 30
                    }
                };

                var marginSetter = windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == Border.MarginProperty) as Setter;
                if (marginSetter == null)
                {
                    var margin = new Thickness(ShadowExtraMargin, 12, ShadowExtraMargin, ShadowExtraMargin);
                    marginSetter = new Setter()
                    {
                        Property = Border.MarginProperty,
                        Value = margin,
                    };
                    windowBorderStyle.Setters.Add(marginSetter);

                    SetResizeBoarderThickness(margin);
                }
                else
                {
                    var baseMargin = (Thickness)marginSetter.Value;
                    var newMargin = new Thickness(
                        baseMargin.Left + ShadowExtraMargin,
                        baseMargin.Top + ShadowExtraMargin,
                        baseMargin.Right + ShadowExtraMargin,
                        baseMargin.Bottom + ShadowExtraMargin);
                    marginSetter.Value = newMargin;

                    SetResizeBoarderThickness(newMargin);
                }

                windowBorderStyle.Setters.Add(effectSetter);

                UpdateResourceDictionary(dict);

        }

        public void RemoveDropShadowEffectFromCurrentTheme()
        {
        
            var dict = GetCurrentResourceDictionary();
            //mainWindow.WindowStyle = WindowStyle.None;
            var windowBorderStyle = dict["WindowBorderStyle"] as Style;

            var effectSetter = windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == Border.EffectProperty) as Setter;
            var marginSetter = windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == Border.MarginProperty) as Setter;

            if (effectSetter != null)
            {
                windowBorderStyle.Setters.Remove(effectSetter);
            }
            if (marginSetter != null)
            {
                var currentMargin = (Thickness)marginSetter.Value;
                var newMargin = new Thickness(
                    currentMargin.Left - ShadowExtraMargin,
                    currentMargin.Top - ShadowExtraMargin,
                    currentMargin.Right - ShadowExtraMargin,
                    currentMargin.Bottom - ShadowExtraMargin);
                marginSetter.Value = newMargin;
            }

            SetResizeBoarderThickness(null);

            UpdateResourceDictionary(dict);
        }

        // because adding drop shadow effect will change the margin of the window,
        // we need to update the window chrome thickness to correct set the resize border
        private static void SetResizeBoarderThickness(Thickness? effectMargin)
        {
            var window = Application.Current.MainWindow;
            if (WindowChrome.GetWindowChrome(window) is WindowChrome windowChrome)
            {
                Thickness thickness;
                if (effectMargin == null)
                {
                    thickness = SystemParameters.WindowResizeBorderThickness;
                }
                else
                {
                    thickness = new Thickness(
                        effectMargin.Value.Left + SystemParameters.WindowResizeBorderThickness.Left,
                        effectMargin.Value.Top + SystemParameters.WindowResizeBorderThickness.Top,
                        effectMargin.Value.Right + SystemParameters.WindowResizeBorderThickness.Right,
                        effectMargin.Value.Bottom + SystemParameters.WindowResizeBorderThickness.Bottom);
                }

                windowChrome.ResizeBorderThickness = thickness;
            }
        }

        public record ThemeData(string FileNameWithoutExtension, string Name, bool? IsDark = null, bool? HasBlur = null);
    }
}
