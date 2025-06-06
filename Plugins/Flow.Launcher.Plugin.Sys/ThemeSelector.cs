using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.Sys
{
    public class ThemeSelector
    {
        public const string Keyword = "fltheme";

        private readonly PluginInitContext _context;

        public ThemeSelector(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            var themes = _context.API.GetAvailableThemes();
            var selectedTheme = _context.API.GetCurrentTheme();

            var search = query.SecondToEndSearch;
            if (string.IsNullOrWhiteSpace(search))
            {
                return themes.Select(x => CreateThemeResult(x, selectedTheme))
                    .OrderBy(x => x.Title)
                    .ToList();
            }

            return themes.Select(theme => (theme, matchResult: _context.API.FuzzySearch(search, theme.Name)))
                .Where(x => x.matchResult.IsSearchPrecisionScoreMet())
                .Select(x => CreateThemeResult(x.theme, selectedTheme, x.matchResult.Score, x.matchResult.MatchData))
                .OrderBy(x => x.Title)
                .ToList();
        }

        private Result CreateThemeResult(ThemeData theme, ThemeData selectedTheme) => CreateThemeResult(theme, selectedTheme, 0, null);

        private Result CreateThemeResult(ThemeData theme, ThemeData selectedTheme, int score, IList<int> highlightData)
        {
            string title;
            if (theme == selectedTheme)
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
                    if (_context.API.SetCurrentTheme(theme))
                    {
                        _context.API.ReQuery();
                    }
                    return false;
                }
            };
        }
    }
}
