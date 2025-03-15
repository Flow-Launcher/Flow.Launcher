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
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

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

        public string CurrentTheme => _settings.Theme;

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

            [DllImport("dwmapi.dll")]
            static extern int DwmSetWindowAttribute(IntPtr hwnd, ParameterTypes.DWMWINDOWATTRIBUTE dwAttribute, ref int pvAttribute, int cbAttribute);

            public static int SetWindowAttribute(IntPtr hwnd, ParameterTypes.DWMWINDOWATTRIBUTE attribute, int parameter)
                => DwmSetWindowAttribute(hwnd, attribute, ref parameter, Marshal.SizeOf<int>());
        }

        private System.Windows.Window GetMainWindow()
        {
            return Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow);
        }


        public void RefreshFrame()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Window mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                    return;

                IntPtr mainWindowPtr = new WindowInteropHelper(mainWindow).Handle;
                if (mainWindowPtr == IntPtr.Zero)
                    return;

                HwndSource mainWindowSrc = HwndSource.FromHwnd(mainWindowPtr);
                if (mainWindowSrc == null)
                    return;

                // Remove OS minimizing/maximizing animation
                // Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, 3);

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
            }, DispatcherPriority.Normal);
        }



        public void AutoDropShadow()
        {
            SetWindowCornerPreference("Default");
            RemoveDropShadowEffectFromCurrentTheme();
            if (_settings.UseDropShadowEffect)
            {
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Window mainWindow = GetMainWindow();
                if (mainWindow == null)
                    return;

                DWM_WINDOW_CORNER_PREFERENCE preference = cornerType switch
                {
                    "DoNotRound" => DWM_WINDOW_CORNER_PREFERENCE.DoNotRound,
                    "Round" => DWM_WINDOW_CORNER_PREFERENCE.Round,
                    "RoundSmall" => DWM_WINDOW_CORNER_PREFERENCE.RoundSmall,
                    "Default" => DWM_WINDOW_CORNER_PREFERENCE.Default,
                    _ => DWM_WINDOW_CORNER_PREFERENCE.Default,
                };

                SetWindowCornerPreference(mainWindow, preference);
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// Sets the blur for a window via SetWindowCompositionAttribute
        /// </summary>
        public void SetBlurForWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dict = GetThemeResourceDictionary(_settings.Theme);
                if (dict == null)
                    return;

                var windowBorderStyle = dict.Contains("WindowBorderStyle") ? dict["WindowBorderStyle"] as Style : null;
                if (windowBorderStyle == null)
                    return;

                System.Windows.Window mainWindow = GetMainWindow();
                if (mainWindow == null)
                    return;

                //  Check if the theme supports blur
                bool hasBlur = dict.Contains("ThemeBlurEnabled") && dict["ThemeBlurEnabled"] is bool b && b;
                if (!hasBlur)
                {
                    _settings.BackdropType = BackdropTypes.None;
                }

                // Check the configured BackdropType
                int backdropValue = _settings.BackdropType switch
                {
                    BackdropTypes.Acrylic => 3, // Acrylic
                    BackdropTypes.Mica => 2,    // Mica
                    BackdropTypes.MicaAlt => 4, // MicaAlt
                    _ => 0                      // None
                };

                if (BlurEnabled && hasBlur)
                {
                    //  If the BackdropType is Mica or MicaAlt, set the windowborderstyle's background to transparent
                    if (_settings.BackdropType == BackdropTypes.Mica || _settings.BackdropType == BackdropTypes.MicaAlt)
                    {
                        windowBorderStyle.Setters.Remove(windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property.Name == "Background"));
                        windowBorderStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))));
                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdropValue);
                        ColorizeWindow(GetSystemBG());
                    }
                    else if (_settings.BackdropType == BackdropTypes.Acrylic)
                    {
                        windowBorderStyle.Setters.Remove(windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property.Name == "Background"));
                        windowBorderStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdropValue);
                        ColorizeWindow(GetSystemBG());
                    }
                    else
                    {
                        Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, backdropValue);
                        ColorizeWindow(GetSystemBG());
                    }
                }
                else
                {
                    //  Apply default style when Blur is disabled
                    Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, 0);
                    ColorizeWindow(GetSystemBG());
                }

                UpdateResourceDictionary(dict);
            }, DispatcherPriority.Normal);
        }




        // Get Background Color from WindowBorderStyle when there not color for BG.
        // for theme has not "LightBG" or "DarkBG" case.
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

        private void ApplyPreviewBackground(Color? bgColor = null)
        {
            if (bgColor == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Copy the existing WindowBorderStyle
                var previewStyle = new Style(typeof(Border));
                if (Application.Current.Resources.Contains("WindowBorderStyle"))
                {
                    var originalStyle = Application.Current.Resources["WindowBorderStyle"] as Style;
                    if (originalStyle != null)
                    {
                        foreach (var setter in originalStyle.Setters.OfType<Setter>())
                        {
                            previewStyle.Setters.Add(new Setter(setter.Property, setter.Value));
                        }
                    }
                }

                // Apply background color (remove transparency in color)
                // WPF does not allow the use of an acrylic brush within the window's internal area,
                // so transparency effects are not applied to the preview.
                Color backgroundColor = Color.FromRgb(bgColor.Value.R, bgColor.Value.G, bgColor.Value.B);
                previewStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(backgroundColor)));

                // The blur theme keeps the corner round fixed (applying DWM code to modify it causes rendering issues).
                // The non-blur theme retains the previously set WindowBorderStyle.
                if (BlurEnabled)
                {
                    previewStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(5)));
                    previewStyle.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
                }
                Application.Current.Resources["PreviewWindowBorderStyle"] = previewStyle;
            }, DispatcherPriority.Render);
        }


        public void ColorizeWindow(string Mode)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dict = GetThemeResourceDictionary(_settings.Theme);
                if (dict == null) return;

                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;

                // Check if the theme supports blur
                bool hasBlur = dict.Contains("ThemeBlurEnabled") && dict["ThemeBlurEnabled"] is bool b && b;

                // SystemBG value check (Auto, Light, Dark)
                string systemBG = dict.Contains("SystemBG") ? dict["SystemBG"] as string : "Auto"; // 기본값 Auto

                // Check the user's ColorScheme setting
                string colorScheme = _settings.ColorScheme;

                // Check system dark mode setting (read AppsUseLightTheme value)
                int themeValue = (int)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                bool isSystemDark = themeValue == 0;

                // Final decision on whether to use dark mode
                bool useDarkMode = false;

                if (colorScheme == "Dark" || systemBG == "Dark")
                {
                    useDarkMode = true;  // Dark
                }
                else if (colorScheme == "Light" || systemBG == "Light")
                {
                    useDarkMode = false; // Light
                }
                else if (colorScheme == "System" || systemBG == "Auto")
                {
                    useDarkMode = isSystemDark; // Auto
                }
                
                // Apply DWM Dark Mode 
                Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, useDarkMode ? 1 : 0);

                Color LightBG;
                Color DarkBG;

                // Retrieve LightBG value (fallback to WindowBorderStyle background color if not found)
                try
                {
                    LightBG = dict.Contains("LightBG") ? (Color)dict["LightBG"] : GetWindowBorderStyleBackground();
                }
                catch (Exception)
                {
                    LightBG = GetWindowBorderStyleBackground();
                }

                // Retrieve DarkBG value (fallback to LightBG if not found)
                try
                {
                    DarkBG = dict.Contains("DarkBG") ? (Color)dict["DarkBG"] : LightBG;
                }
                catch (Exception)
                {
                    DarkBG = LightBG;
                }

                // Select background color based on ColorScheme and SystemBG
                Color selectedBG = useDarkMode ? DarkBG : LightBG;
                ApplyPreviewBackground(selectedBG);

                if (!hasBlur)
                {
                    mainWindow.Background = Brushes.Transparent;
                }
                else
                {
                    // Only set the background to transparent if the theme supports blur
                    if (_settings.BackdropType == BackdropTypes.Mica || _settings.BackdropType == BackdropTypes.MicaAlt)
                    {
                        mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                    }
                    else
                    {
                        mainWindow.Background = new SolidColorBrush(selectedBG);
                    }
                }
            }, DispatcherPriority.Normal);
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
        public string GetSystemBG()
        {
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                var resource = Application.Current.TryFindResource("SystemBG");

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
                //if (_settings.UseDropShadowEffect)
                // AddDropShadowEffectToCurrentTheme();



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
