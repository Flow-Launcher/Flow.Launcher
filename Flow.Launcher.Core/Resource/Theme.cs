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
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core.Resource
{
    public class Theme
    {
        private const string ThemeMetadataNamePrefix = "Name:";
        private const string ThemeMetadataIsDarkPrefix = "IsDark:";
        private const string ThemeMetadataHasBlurPrefix = "HasBlur:";

        private const int ShadowExtraMargin = 32;

        private readonly List<string> _themeDirectories = new List<string>();
        private ResourceDictionary _oldResource;
        private string _oldTheme;
        public Settings Settings { get; set; }
        private const string Folder = Constant.Themes;
        private const string Extension = ".xaml";
        private string DirectoryPath => Path.Combine(Constant.ProgramDirectory, Folder);
        private string UserDirectoryPath => Path.Combine(DataLocation.DataDirectory(), Folder);

        public bool BlurEnabled { get; set; }

        private double mainWindowWidth;

        public Theme()
        {
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

                Settings.Theme = theme;


                //always allow re-loading default theme, in case of failure of switching to a new theme from default theme
                if (_oldTheme != theme || theme == defaultTheme)
                {
                    _oldTheme = Path.GetFileNameWithoutExtension(_oldResource.Source.AbsolutePath);
                }

                BlurEnabled = Win32Helper.IsBlurTheme();

                if (Settings.UseDropShadowEffect && !BlurEnabled)
                    AddDropShadowEffectToCurrentTheme();

                Win32Helper.SetBlurForWindow(Application.Current.MainWindow, BlurEnabled);
            }
            catch (DirectoryNotFoundException)
            {
                Log.Error($"|Theme.ChangeTheme|Theme <{theme}> path can't be found");
                if (theme != defaultTheme)
                {
                    MessageBoxEx.Show(string.Format(InternationalizationManager.Instance.GetTranslation("theme_load_failure_path_not_exists"), theme));
                    ChangeTheme(defaultTheme);
                }
                return false;
            }
            catch (XamlParseException)
            {
                Log.Error($"|Theme.ChangeTheme|Theme <{theme}> fail to parse");
                if (theme != defaultTheme)
                {
                    MessageBoxEx.Show(string.Format(InternationalizationManager.Instance.GetTranslation("theme_load_failure_parse_error"), theme));
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

        private ResourceDictionary CurrentThemeResourceDictionary() => GetThemeResourceDictionary(Settings.Theme);

        public ResourceDictionary GetResourceDictionary(string theme)
        {
            var dict = GetThemeResourceDictionary(theme);

            if (dict["QueryBoxStyle"] is Style queryBoxStyle &&
                dict["QuerySuggestionBoxStyle"] is Style querySuggestionBoxStyle)
            {
                var fontFamily = new FontFamily(Settings.QueryBoxFont);
                var fontStyle = FontHelper.GetFontStyleFromInvariantStringOrNormal(Settings.QueryBoxFontStyle);
                var fontWeight = FontHelper.GetFontWeightFromInvariantStringOrNormal(Settings.QueryBoxFontWeight);
                var fontStretch = FontHelper.GetFontStretchFromInvariantStringOrNormal(Settings.QueryBoxFontStretch);

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
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(Settings.ResultFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(Settings.ResultFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(Settings.ResultFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(Settings.ResultFontStretch));

                Setter[] setters = { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(
                    new[] { resultItemStyle, resultItemSelectedStyle, resultHotkeyItemStyle, resultHotkeyItemSelectedStyle }, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            if (
                dict["ItemSubTitleStyle"] is Style resultSubItemStyle &&
                dict["ItemSubTitleSelectedStyle"] is Style resultSubItemSelectedStyle)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(Settings.ResultSubFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(Settings.ResultSubFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(Settings.ResultSubFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(Settings.ResultSubFontStretch));

                Setter[] setters = { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(
                    new[] {  resultSubItemStyle,resultSubItemSelectedStyle}, o
                    => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            /* Ignore Theme Window Width and use setting */
            var windowStyle = dict["WindowStyle"] as Style;
            var width = Settings.WindowSize;
            windowStyle.Setters.Add(new Setter(Window.WidthProperty, width));
            mainWindowWidth = (double)width;
            return dict;
        }

        private ResourceDictionary GetCurrentResourceDictionary( )
        {
            return  GetResourceDictionary(Settings.Theme);
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
                marginSetter = new Setter()
                {
                    Property = Border.MarginProperty,
                    Value = new Thickness(ShadowExtraMargin, 12, ShadowExtraMargin, ShadowExtraMargin),
                };
                windowBorderStyle.Setters.Add(marginSetter);
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

            UpdateResourceDictionary(dict);
        }

        public record ThemeData(string FileNameWithoutExtension, string Name, bool? IsDark = null, bool? HasBlur = null);
    }
}
