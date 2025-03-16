using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Plugin.Sys
{
    public class ThemeSelector
    {
        public const string Keyword = "fltheme";

        private readonly Settings _settings;
        private readonly Theme _theme;
        private readonly PluginInitContext _context;

        #region Theme Selection

        // Theme select codes simplified from SettingsPaneThemeViewModel.cs

        private Theme.ThemeData _selectedTheme;
        public Theme.ThemeData SelectedTheme
        {
            get => _selectedTheme ??= Themes.Find(v => v.FileNameWithoutExtension == _theme.CurrentTheme);
            set
            {
                _selectedTheme = value;
                _theme.ChangeTheme(value.FileNameWithoutExtension);

                // when changed non-blur theme, change to backdrop to none
                if (!_theme.BlurEnabled)
                {
                    _settings.BackdropType = BackdropTypes.None;
                }

                // dropshadow on and control disabled.(user can't change dropshadow with blur theme)
                if (_theme.BlurEnabled)
                {
                    _settings.UseDropShadowEffect = true;
                }

                _theme.RefreshFrame();
            }
        }

        private List<Theme.ThemeData> Themes => _theme.LoadAvailableThemes();

        #endregion

        public ThemeSelector(PluginInitContext context)
        {
            _context = context;
            _theme = Ioc.Default.GetRequiredService<Theme>();
            _settings = Ioc.Default.GetRequiredService<Settings>();
        }

        public List<Result> Query(Query query)
        {
            var search = query.SecondToEndSearch;
            if (string.IsNullOrWhiteSpace(search))
            {
                return Themes.Select(CreateThemeResult)
                    .OrderBy(x => x.Title)
                    .ToList();
            }

            return Themes.Select(theme => (theme, matchResult: _context.API.FuzzySearch(search, theme.Name)))
                .Where(x => x.matchResult.IsSearchPrecisionScoreMet())
                .Select(x => CreateThemeResult(x.theme, x.matchResult.Score, x.matchResult.MatchData))
                .OrderBy(x => x.Title)
                .ToList();
        }

        private Result CreateThemeResult(Theme.ThemeData theme) => CreateThemeResult(theme, 0, null);

        private Result CreateThemeResult(Theme.ThemeData theme, int score, IList<int> highlightData)
        {
            string themeName = theme.Name;
            string title;
            if (theme == SelectedTheme)
            {
                title = $"{theme.Name} ★";
                // Set current theme to the top
                score = 2000;
            }
            else
            {
                title = theme.Name;
                // Set them to 1000 so that they are higher than other non-theme records
                score = 1000;
            }

            string description = string.Empty;
            if (theme.IsDark == true)
            {
                description += _context.API.GetTranslation("TypeIsDarkToolTip");
            }

            if (theme.HasBlur == true)
            {
                if (!string.IsNullOrEmpty(description))
                    description += " ";
                description += _context.API.GetTranslation("TypeHasBlurToolTip");
            }

            return new Result
            {
                Title = title,
                TitleHighlightData = highlightData,
                SubTitle = description,
                IcoPath = "Images\\theme_selector.png",
                Glyph = new GlyphInfo("/Resources/#Segoe Fluent Icons", "\ue790"),
                Score = score,
                Action = c =>
                {
                    SelectedTheme = theme;
                    _context.API.ReQuery();
                    return false;
                }
            };
        }
    }
}
