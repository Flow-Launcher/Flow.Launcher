using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.Sys
{
    public static class ThemeSelector
    {
        public const string Keyword = "fltheme";

        public static List<Result> Query(Query query)
        {
            var themes = Main.Context.API.GetAvailableThemes();
            var selectedTheme = Main.Context.API.GetCurrentTheme();

            var search = query.SecondToEndSearch;
            if (string.IsNullOrWhiteSpace(search))
            {
                return [.. themes.Select(x => CreateThemeResult(x, selectedTheme)).OrderBy(x => x.Title)];
            }

            return [.. themes.Select(theme => (theme, matchResult: Main.Context.API.FuzzySearch(search, theme.Name)))
            .Where(x => x.matchResult.IsSearchPrecisionScoreMet())
            .Select(x => CreateThemeResult(x.theme, selectedTheme, x.matchResult.Score, x.matchResult.MatchData))
            .OrderBy(x => x.Title)];
        }

        private static Result CreateThemeResult(ThemeData theme, ThemeData selectedTheme) => CreateThemeResult(theme, selectedTheme, 0, null);

        private static Result CreateThemeResult(ThemeData theme, ThemeData selectedTheme, int score, IList<int> highlightData)
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

            string description;
            if (theme.IsDark == true)
            {
                if (theme.HasBlur == true)
                {
                    description = Localize.flowlauncher_plugin_sys_type_isdark_hasblur();
                }
                else
                {
                    description = Localize.flowlauncher_plugin_sys_type_isdark();
                }
            }
            else
            {
                if (theme.HasBlur == true)
                {
                    description = Localize.flowlauncher_plugin_sys_type_hasblur();
                }
                else
                {
                    description = string.Empty;
                }
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
                    if (Main.Context.API.SetCurrentTheme(theme))
                    {
                        Main.Context.API.ReQuery();
                    }
                    return false;
                }
            };
        }
    }
}
