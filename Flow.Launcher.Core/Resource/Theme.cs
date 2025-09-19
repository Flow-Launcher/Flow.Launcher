using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Microsoft.Win32;

namespace Flow.Launcher.Core.Resource
{
    public class Theme
    {
        #region Properties & Fields

        private readonly string ClassName = nameof(Theme);

        public bool BlurEnabled { get; private set; }

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
        private static string DirectoryPath => Path.Combine(Constant.ProgramDirectory, Folder);
        private static string UserDirectoryPath => Path.Combine(DataLocation.DataDirectory(), Folder);

        private Thickness _themeResizeBorderThickness;

        #endregion

        #region Constructor

        public Theme(IPublicAPI publicAPI, Settings settings)
        {
            _api = publicAPI;
            _settings = settings;

            _themeDirectories.Add(DirectoryPath);
            _themeDirectories.Add(UserDirectoryPath);
            MakeSureThemeDirectoriesExist();

            var dicts = Application.Current.Resources.MergedDictionaries;
            _oldResource = dicts.FirstOrDefault(d =>
            {
                if (d.Source == null) return false;

                var p = d.Source.AbsolutePath;
                return p.Contains(Folder) && Path.GetExtension(p) == Extension;
            });

            if (_oldResource != null)
            {
                _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
            }
            else
            {
                _api.LogError(ClassName, "Current theme resource not found. Initializing with default theme.");
                _oldTheme = Constant.DefaultTheme;
            }
        }

        #endregion

        #region Theme Resources

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
                    _api.LogException(ClassName, $"Exception when create directory <{dir}>", e);
                }
            }
        }

        private void UpdateResourceDictionary(ResourceDictionary dictionaryToUpdate)
        {
            // Add new resources
            if (!Application.Current.Resources.MergedDictionaries.Contains(dictionaryToUpdate))
            {
                Application.Current.Resources.MergedDictionaries.Add(dictionaryToUpdate);
            }

            // Remove old resources
            if (_oldResource != null && _oldResource != dictionaryToUpdate &&
                Application.Current.Resources.MergedDictionaries.Contains(_oldResource))
            {
                Application.Current.Resources.MergedDictionaries.Remove(_oldResource);
            }

            _oldResource = dictionaryToUpdate;
        }

        /// <summary>
        /// Updates only the font settings and refreshes the UI.
        /// </summary>
        public void UpdateFonts()
        {
            try
            {
                // Load a ResourceDictionary for the specified theme.
                var themeName = _settings.Theme;
                var dict = GetThemeResourceDictionary(themeName);

                // Apply font settings to the theme resource.
                ApplyFontSettings(dict);
                UpdateResourceDictionary(dict);

                // Must apply blur and drop shadow effects
                _ = RefreshFrameAsync();
            }
            catch (Exception e)
            {
                _api.LogException(ClassName, "Error occurred while updating theme fonts", e);
            }
        }

        /// <summary>
        /// Loads and applies font settings to the theme resource.
        /// </summary>
        private void ApplyFontSettings(ResourceDictionary dict)
        {
            if (dict["QueryBoxStyle"] is Style queryBoxStyle &&
                dict["QuerySuggestionBoxStyle"] is Style querySuggestionBoxStyle)
            {
                var fontFamily = new FontFamily(_settings.QueryBoxFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.QueryBoxFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.QueryBoxFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.QueryBoxFontStretch);

                SetFontProperties(queryBoxStyle, fontFamily, fontStyle, fontWeight, fontStretch, true);
                SetFontProperties(querySuggestionBoxStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
            }

            if (dict["ItemTitleStyle"] is Style resultItemStyle &&
                dict["ItemTitleSelectedStyle"] is Style resultItemSelectedStyle &&
                dict["ItemHotkeyStyle"] is Style resultHotkeyItemStyle &&
                dict["ItemHotkeySelectedStyle"] is Style resultHotkeyItemSelectedStyle)
            {
                var fontFamily = new FontFamily(_settings.ResultFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultFontStretch);

                SetFontProperties(resultItemStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
                SetFontProperties(resultItemSelectedStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
                SetFontProperties(resultHotkeyItemStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
                SetFontProperties(resultHotkeyItemSelectedStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
            }

            if (dict["ItemSubTitleStyle"] is Style resultSubItemStyle &&
                dict["ItemSubTitleSelectedStyle"] is Style resultSubItemSelectedStyle)
            {
                var fontFamily = new FontFamily(_settings.ResultSubFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.ResultSubFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.ResultSubFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.ResultSubFontStretch);

                SetFontProperties(resultSubItemStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
                SetFontProperties(resultSubItemSelectedStyle, fontFamily, fontStyle, fontWeight, fontStretch, false);
            }
        }

        /// <summary>
        /// Applies font properties to a Style.
        /// </summary>
        private static void SetFontProperties(Style style, FontFamily fontFamily, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, bool isTextBox)
        {
            // Remove existing font-related setters  
            if (isTextBox)
            {
                //  First, find the setters to remove and store them in a list  
                var settersToRemove = style.Setters
                    .OfType<Setter>()
                    .Where(setter =>
                        setter.Property == Control.FontFamilyProperty ||
                        setter.Property == Control.FontStyleProperty ||
                        setter.Property == Control.FontWeightProperty ||
                        setter.Property == Control.FontStretchProperty)
                    .ToList();

                // Remove each found setter one by one  
                foreach (var setter in settersToRemove)
                {
                    style.Setters.Remove(setter);
                }

                // Add New font setter
                style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
                style.Setters.Add(new Setter(Control.FontStyleProperty, fontStyle));
                style.Setters.Add(new Setter(Control.FontWeightProperty, fontWeight));
                style.Setters.Add(new Setter(Control.FontStretchProperty, fontStretch));

                //  Set caret brush (retain existing logic)
                var caretBrushPropertyValue = style.Setters.OfType<Setter>().Any(x => x.Property.Name == "CaretBrush");
                var foregroundPropertyValue = style.Setters.OfType<Setter>().Where(x => x.Property.Name == "Foreground")
                    .Select(x => x.Value).FirstOrDefault();
                if (!caretBrushPropertyValue && foregroundPropertyValue != null)
                    style.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, foregroundPropertyValue));
            }
            else
            {
                var settersToRemove = style.Setters
                    .OfType<Setter>()
                    .Where(setter =>
                        setter.Property == TextBlock.FontFamilyProperty ||
                        setter.Property == TextBlock.FontStyleProperty ||
                        setter.Property == TextBlock.FontWeightProperty ||
                        setter.Property == TextBlock.FontStretchProperty)
                    .ToList();

                foreach (var setter in settersToRemove)
                {
                    style.Setters.Remove(setter);
                }

                style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, fontFamily));
                style.Setters.Add(new Setter(TextBlock.FontStyleProperty, fontStyle));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, fontWeight));
                style.Setters.Add(new Setter(TextBlock.FontStretchProperty, fontStretch));
            }
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

        private ResourceDictionary GetResourceDictionary(string theme)
        {
            var dict = GetThemeResourceDictionary(theme);

            if (dict["QueryBoxStyle"] is Style queryBoxStyle &&
                dict["QuerySuggestionBoxStyle"] is Style querySuggestionBoxStyle)
            {
                var fontFamily = new FontFamily(_settings.QueryBoxFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(_settings.QueryBoxFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(_settings.QueryBoxFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(_settings.QueryBoxFontStretch);

                queryBoxStyle.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
                queryBoxStyle.Setters.Add(new Setter(Control.FontStyleProperty, fontStyle));
                queryBoxStyle.Setters.Add(new Setter(Control.FontWeightProperty, fontWeight));
                queryBoxStyle.Setters.Add(new Setter(Control.FontStretchProperty, fontStretch));

                var caretBrushPropertyValue = queryBoxStyle.Setters.OfType<Setter>().Any(x => x.Property.Name == "CaretBrush");
                var foregroundPropertyValue = queryBoxStyle.Setters.OfType<Setter>().Where(x => x.Property.Name == "Foreground")
                    .Select(x => x.Value).FirstOrDefault();
                if (!caretBrushPropertyValue && foregroundPropertyValue != null) //otherwise BaseQueryBoxStyle will handle styling
                    queryBoxStyle.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, foregroundPropertyValue));

                // Query suggestion box's font style is aligned with query box
                querySuggestionBoxStyle.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
                querySuggestionBoxStyle.Setters.Add(new Setter(Control.FontStyleProperty, fontStyle));
                querySuggestionBoxStyle.Setters.Add(new Setter(Control.FontWeightProperty, fontWeight));
                querySuggestionBoxStyle.Setters.Add(new Setter(Control.FontStretchProperty, fontStretch));
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
                    new[] { resultSubItemStyle, resultSubItemSelectedStyle }, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            /* Ignore Theme Window Width and use setting */
            var windowStyle = dict["WindowStyle"] as Style;
            var width = _settings.WindowSize;
            windowStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return dict;
        }

        public ResourceDictionary GetCurrentResourceDictionary()
        {
            return GetResourceDictionary(_settings.Theme);
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

        #endregion

        #region Get & Change Theme

        public ThemeData GetCurrentTheme()
        {
            var themes = GetAvailableThemes();
            var matchingTheme = themes.FirstOrDefault(t => t.FileNameWithoutExtension == _settings.Theme);
            if (matchingTheme == null)
            {
                _api.LogWarn(ClassName, $"No matching theme found for '{_settings.Theme}'. Falling back to the first available theme.");
            }
            return matchingTheme ?? themes.FirstOrDefault();
        }

        public List<ThemeData> GetAvailableThemes()
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

        public bool ChangeTheme(string theme = null)
        {
            if (string.IsNullOrEmpty(theme))
                theme = _settings.Theme;

            string path = GetThemePath(theme);
            try
            {
                if (string.IsNullOrEmpty(path))
                    throw new DirectoryNotFoundException($"Theme path can't be found <{path}>");

                // Retrieve theme resource – always use the resource with font settings applied.
                var resourceDict = GetResourceDictionary(theme);

                UpdateResourceDictionary(resourceDict);

                _settings.Theme = theme;

                //always allow re-loading default theme, in case of failure of switching to a new theme from default theme
                if (_oldTheme != theme || theme == Constant.DefaultTheme)
                {
                    _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
                }

                BlurEnabled = IsBlurTheme();

                // Apply blur and drop shadow effect so that we do not need to call it again
                _ = RefreshFrameAsync();

                return true;
            }
            catch (DirectoryNotFoundException)
            {
                _api.LogError(ClassName, $"Theme <{theme}> path can't be found");
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(string.Format(_api.GetTranslation("theme_load_failure_path_not_exists"), theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (XamlParseException)
            {
                _api.LogError(ClassName, $"Theme <{theme}> fail to parse");
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(string.Format(_api.GetTranslation("theme_load_failure_parse_error"), theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
        }

        #endregion

        #region Shadow Effect

        public void AddDropShadowEffectToCurrentTheme()
        {
            var dict = GetCurrentResourceDictionary();

            var windowBorderStyle = dict["WindowBorderStyle"] as Style;

            var effectSetter = new Setter
            {
                Property = UIElement.EffectProperty,
                Value = new DropShadowEffect
                {
                    Opacity = 0.3,
                    ShadowDepth = 12,
                    Direction = 270,
                    BlurRadius = 30
                }
            };

            if (windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == FrameworkElement.MarginProperty) is not Setter marginSetter)
            {
                var margin = new Thickness(ShadowExtraMargin, 12, ShadowExtraMargin, ShadowExtraMargin);
                marginSetter = new Setter()
                {
                    Property = FrameworkElement.MarginProperty,
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

            if (windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == UIElement.EffectProperty) is Setter effectSetter)
            {
                windowBorderStyle.Setters.Remove(effectSetter);
            }

            if (windowBorderStyle.Setters.FirstOrDefault(setterBase => setterBase is Setter setter && setter.Property == FrameworkElement.MarginProperty) is Setter marginSetter)
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

        public void SetResizeBorderThickness(WindowChrome windowChrome, bool fixedWindowSize)
        {
            if (fixedWindowSize)
            {
                windowChrome.ResizeBorderThickness = new Thickness(0);
            }
            else
            {
                windowChrome.ResizeBorderThickness = _themeResizeBorderThickness;
            }
        }

        // because adding drop shadow effect will change the margin of the window,
        // we need to update the window chrome thickness to correct set the resize border
        private void SetResizeBoarderThickness(Thickness? effectMargin)
        {
            var window = Application.Current.MainWindow;
            if (WindowChrome.GetWindowChrome(window) is WindowChrome windowChrome)
            {
                // Save the theme resize border thickness so that we can restore it if we change ResizeWindow setting
                if (effectMargin == null)
                {
                    _themeResizeBorderThickness = SystemParameters.WindowResizeBorderThickness;
                }
                else
                {
                    _themeResizeBorderThickness = new Thickness(
                        effectMargin.Value.Left + SystemParameters.WindowResizeBorderThickness.Left,
                        effectMargin.Value.Top + SystemParameters.WindowResizeBorderThickness.Top,
                        effectMargin.Value.Right + SystemParameters.WindowResizeBorderThickness.Right,
                        effectMargin.Value.Bottom + SystemParameters.WindowResizeBorderThickness.Bottom);
                }

                // Apply the resize border thickness to the window chrome
                SetResizeBorderThickness(windowChrome, _settings.KeepMaxResults);
            }
        }

        #endregion

        #region Blur Handling

        /// <summary>
        /// Refreshes the frame to apply the current theme settings.
        /// </summary>
        public async Task RefreshFrameAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Get the actual backdrop type and drop shadow effect settings
                var (backdropType, useDropShadowEffect) = GetActualValue();

                // Remove OS minimizing/maximizing animation
                // Methods.SetWindowAttribute(new WindowInteropHelper(mainWindow).Handle, DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, 3);

                // The timing of adding the shadow effect should vary depending on whether the theme is transparent.
                if (BlurEnabled)
                {
                    AutoDropShadow(useDropShadowEffect);
                }
                SetBlurForWindow(_settings.Theme, backdropType);

                if (!BlurEnabled)
                {
                    AutoDropShadow(useDropShadowEffect);
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Sets the blur for a window via SetWindowCompositionAttribute
        /// </summary>
        public async Task SetBlurForWindowAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Get the actual backdrop type and drop shadow effect settings
                var (backdropType, _) = GetActualValue();

                SetBlurForWindow(_settings.Theme, backdropType);
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Gets the actual backdrop type and drop shadow effect settings based on the current theme status.
        /// </summary>
        public (BackdropTypes BackdropType, bool UseDropShadowEffect) GetActualValue()
        {
            var backdropType = _settings.BackdropType;
            var useDropShadowEffect = _settings.UseDropShadowEffect;

            // When changed non-blur theme, change to backdrop to none
            if (!BlurEnabled)
            {
                backdropType = BackdropTypes.None;
            }

            // Dropshadow on and control disabled.(user can't change dropshadow with blur theme)
            if (BlurEnabled)
            {
                useDropShadowEffect = true;
            }

            return (backdropType, useDropShadowEffect);
        }

        private void SetBlurForWindow(string theme, BackdropTypes backdropType)
        {
            var dict = GetResourceDictionary(theme);
            if (dict == null) return;

            var windowBorderStyle = dict.Contains("WindowBorderStyle") ? dict["WindowBorderStyle"] as Style : null;
            if (windowBorderStyle == null) return;

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;

            // Check if the theme supports blur
            bool hasBlur = dict.Contains("ThemeBlurEnabled") && dict["ThemeBlurEnabled"] is bool b && b;
            if (BlurEnabled && hasBlur && Win32Helper.IsBackdropSupported())
            {
                // If the BackdropType is Mica or MicaAlt, set the windowborderstyle's background to transparent
                if (backdropType == BackdropTypes.Mica || backdropType == BackdropTypes.MicaAlt)
                {
                    windowBorderStyle.Setters.Remove(windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property.Name == "Background"));
                    windowBorderStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))));
                }
                else if (backdropType == BackdropTypes.Acrylic)
                {
                    windowBorderStyle.Setters.Remove(windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property.Name == "Background"));
                    windowBorderStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
                }
                
                // For themes with blur enabled, the window border is rendered by the system, so it's treated as a simple rectangle regardless of thickness.
                //(This is to avoid issues when the window is forcibly changed to a rectangular shape during snap scenarios.)
                var cornerRadiusSetter = windowBorderStyle.Setters.OfType<Setter>().FirstOrDefault(x => x.Property == Border.CornerRadiusProperty);
                if (cornerRadiusSetter != null)
                    cornerRadiusSetter.Value = new CornerRadius(0);
                else
                    windowBorderStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(0)));
                
                // Apply the blur effect
                Win32Helper.DWMSetBackdropForWindow(mainWindow, backdropType);
                ColorizeWindow(theme, backdropType);
            }
            else
            {
                // Apply default style when Blur is disabled
                Win32Helper.DWMSetBackdropForWindow(mainWindow, BackdropTypes.None);
                ColorizeWindow(theme, backdropType);
            }

            UpdateResourceDictionary(dict);
        }

        private void AutoDropShadow(bool useDropShadowEffect)
        {
            SetWindowCornerPreference("Default");
            RemoveDropShadowEffectFromCurrentTheme();
            if (useDropShadowEffect)
            {
                if (BlurEnabled && Win32Helper.IsBackdropSupported())
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
                if (BlurEnabled && Win32Helper.IsBackdropSupported())
                {
                    SetWindowCornerPreference("Default");
                }
                else
                {
                    RemoveDropShadowEffectFromCurrentTheme();
                }
            }
        }

        private static void SetWindowCornerPreference(string cornerType)
        {
            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return;

            Win32Helper.DWMSetCornerPreferenceForWindow(mainWindow, cornerType);
        }

        // Get Background Color from WindowBorderStyle when there not color for BG.
        // for theme has not "LightBG" or "DarkBG" case.
        private Color GetWindowBorderStyleBackground(string theme)
        {
            var Resources = GetThemeResourceDictionary(theme);
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

            // Create a new Style for the preview
            var previewStyle = new Style(typeof(Border));

            // Get the original WindowBorderStyle
            if (Application.Current.Resources.Contains("WindowBorderStyle") &&
                Application.Current.Resources["WindowBorderStyle"] is Style originalStyle)
            {
                // Copy the original style, including the base style if it exists
                CopyStyle(originalStyle, previewStyle);
            }

            // Apply background color (remove transparency in color)
            Color backgroundColor = Color.FromRgb(bgColor.Value.R, bgColor.Value.G, bgColor.Value.B);
            previewStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(backgroundColor)));

            // The blur theme keeps the corner round fixed (applying DWM code to modify it causes rendering issues).
            // The non-blur theme retains the previously set WindowBorderStyle.
            if (BlurEnabled)
            {
                previewStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(5)));
                previewStyle.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
            }

            // Set the new style to the resource
            Application.Current.Resources["PreviewWindowBorderStyle"] = previewStyle;
        }

        private void CopyStyle(Style originalStyle, Style targetStyle)
        {
            // If the style is based on another style, copy the base style first
            if (originalStyle.BasedOn != null)
            {
                CopyStyle(originalStyle.BasedOn, targetStyle);
            }

            // Copy the setters from the original style
            foreach (var setter in originalStyle.Setters.OfType<Setter>())
            {
                targetStyle.Setters.Add(new Setter(setter.Property, setter.Value));
            }
        }

        private void ColorizeWindow(string theme, BackdropTypes backdropType)
        {
            var dict = GetThemeResourceDictionary(theme);
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

            // If systemBG is not "Auto", prioritize it over ColorScheme and set the mode based on systemBG value
            if (systemBG == "Dark")
            {
                useDarkMode = true;  // Dark
            }
            else if (systemBG == "Light")
            {
                useDarkMode = false; // Light
            }
            else if (systemBG == "Auto")
            {
                // If systemBG is "Auto", decide based on ColorScheme
                if (colorScheme == "Dark")
                    useDarkMode = true;
                else if (colorScheme == "Light")
                    useDarkMode = false;
                else
                    useDarkMode = isSystemDark;  // Auto (based on system setting)
            }

            // Apply DWM Dark Mode
            Win32Helper.DWMSetDarkModeForWindow(mainWindow, useDarkMode);

            Color LightBG;
            Color DarkBG;

            // Retrieve LightBG value (fallback to WindowBorderStyle background color if not found)
            try
            {
                LightBG = dict.Contains("LightBG") ? (Color)dict["LightBG"] : GetWindowBorderStyleBackground(theme);
            }
            catch (Exception)
            {
                LightBG = GetWindowBorderStyleBackground(theme);
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

            bool isBlurAvailable = hasBlur && Win32Helper.IsBackdropSupported(); // Windows 11 미만이면 hasBlur를 강제 false

            if (!isBlurAvailable)
            {
                mainWindow.Background = Brushes.Transparent;
            }
            else
            {
                // Only set the background to transparent if the theme supports blur
                if (backdropType == BackdropTypes.Mica || backdropType == BackdropTypes.MicaAlt)
                {
                    mainWindow.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                }
                else
                {
                    mainWindow.Background = new SolidColorBrush(selectedBG);
                }
            }
        }

        private static bool IsBlurTheme()
        {
            if (!Win32Helper.IsBackdropSupported()) // Windows 11 미만이면 무조건 false
                return false;

            var resource = Application.Current.TryFindResource("ThemeBlurEnabled");

            return resource is bool b && b;
        }

        #endregion
    }
}
