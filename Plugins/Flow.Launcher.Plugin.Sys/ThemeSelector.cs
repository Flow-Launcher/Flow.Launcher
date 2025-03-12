using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Plugin.Sys
{
    public class ThemeSelector
    {
        public const string Keyword = "fltheme";

        private readonly Theme _theme;
        private readonly PluginInitContext _context;

        public ThemeSelector(PluginInitContext context)
        {
            _context = context;
            _theme = Ioc.Default.GetRequiredService<Theme>();
        }

        public List<Result> Query(Query query)
        {
            var themes = _theme.LoadAvailableThemes().Select(x => x.FileNameWithoutExtension);

            string search = query.SecondToEndSearch;

            if (string.IsNullOrWhiteSpace(search))
            {
                return themes.Select(CreateThemeResult)
                    .OrderBy(x => x.Title)
                    .ToList();
            }

            return themes.Select(theme => (theme, matchResult: _context.API.FuzzySearch(search, theme)))
                .Where(x => x.matchResult.IsSearchPrecisionScoreMet())
                .Select(x => CreateThemeResult(x.theme, x.matchResult.Score, x.matchResult.MatchData))
                .OrderBy(x => x.Title)
                .ToList();
        }

        private Result CreateThemeResult(string theme) => CreateThemeResult(theme, 0, null);

        private Result CreateThemeResult(string theme, int score, IList<int> highlightData)
        {
            string title;
            if (theme == _theme.CurrentTheme)
            {
                title = $"{theme} ★";
                // Set current theme to the top
                score = 2000;
            }
            else
            {
                title = theme;
                // Set them to 1000 so that they are higher than other non-theme records
                score = 1000;
            }

            return new Result
            {
                Title = title,
                TitleHighlightData = highlightData,
                IcoPath = "Images\\theme_selector.png",
                Glyph = new GlyphInfo("/Resources/#Segoe Fluent Icons", "\ue790"),
                Score = score,
                Action = c =>
                {
                    _theme.ChangeTheme(theme);
                    return true;
                }
            };
        }
    }
}
