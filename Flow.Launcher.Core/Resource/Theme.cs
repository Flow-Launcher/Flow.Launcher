using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Core.Resource
{
    public partial class Theme
    {
        #region Properties & Fields

        private readonly string ClassName = nameof(Theme);

        public bool BlurEnabled { get; private set; }

        private const string ThemeMetadataNamePrefix = "Name:";
        private const string ThemeMetadataIsDarkPrefix = "IsDark:";
        private const string ThemeMetadataHasBlurPrefix = "HasBlur:";

        private readonly IPublicAPI _api;
        private readonly Settings _settings;
        private readonly List<string> _themeDirectories = new();
        private const string Folder = Constant.Themes;
        private const string Extension = ".xaml";
        private static string DirectoryPath => Path.Combine(Constant.ProgramDirectory, Folder);
        private static string UserDirectoryPath => Path.Combine(DataLocation.DataDirectory(), Folder);

        #endregion

        #region Constructor

        public Theme(IPublicAPI publicAPI, Settings settings)
        {
            _api = publicAPI;
            _settings = settings;

            _themeDirectories.Add(DirectoryPath);
            _themeDirectories.Add(UserDirectoryPath);
            MakeSureThemeDirectoriesExist();

            InitializeThemeResource();
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

        // WPF resource lookup at startup; implemented only in Theme.Windows.cs.
        partial void InitializeThemeResource();

        // WPF resource, blur and drop shadow application; implemented only in Theme.Windows.cs.
        partial void ApplyThemeResource(string theme);

        private static partial bool IsXamlParseException(Exception e);

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
            var themes = new List<ThemeData>();
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
            if (string.IsNullOrEmpty(theme)) theme = _settings.Theme;

            string path = GetThemePath(theme);
            try
            {
                if (string.IsNullOrEmpty(path))
                    throw new DirectoryNotFoundException($"Theme path can't be found <{path}>");

                _settings.Theme = theme;

                ApplyThemeResource(theme);

                return true;
            }
            catch (DirectoryNotFoundException)
            {
                _api.LogError(ClassName, $"Theme <{theme}> path can't be found");
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.theme_load_failure_path_not_exists(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (Exception e) when (IsXamlParseException(e))
            {
                _api.LogException(ClassName, $"Theme <{theme}> fail to parse xaml", e);
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.theme_load_failure_parse_error(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
            catch (Exception e)
            {
                _api.LogException(ClassName, $"Theme <{theme}> fail to load", e);
                if (theme != Constant.DefaultTheme)
                {
                    _api.ShowMsgBox(Localize.theme_load_failure_parse_error(theme));
                    ChangeTheme(Constant.DefaultTheme);
                }
                return false;
            }
        }

        #endregion

        #region Blur Handling

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

        #endregion
    }
}
