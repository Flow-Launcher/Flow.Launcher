using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Plugin.Sys
{
    public class ThemeSelector : IDisposable
    {
        public const string Keyword = "fltheme";

        private readonly PluginInitContext context;
        private IEnumerable<string> themes;

        public ThemeSelector(PluginInitContext context)
        {
            this.context = context;
            context.API.VisibilityChanged += OnVisibilityChanged;
        }

        public List<Result> Query(Query query)
        {
            if (query.IsReQuery)
            {
                LoadThemes();
            }

            int keywordIndex = query.Search.IndexOf(Keyword, StringComparison.Ordinal);
            string search = query.Search[(keywordIndex + Keyword.Length + 1)..];

            if (string.IsNullOrWhiteSpace(search))
            {
                return themes.Select(CreateThemeResult)
                                .OrderBy(x => x.Title)
                                .ToList();
            }

            return themes.Select(theme => (theme, matchResult: context.API.FuzzySearch(search, theme)))
                            .Where(x => x.matchResult.IsSearchPrecisionScoreMet())
                            .Select(x => CreateThemeResult(x.theme, x.matchResult.Score, x.matchResult.MatchData))
                            .OrderBy(x => x.Title)
                            .ToList();
        }

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs args)
        {
            if (args.IsVisible && !context.CurrentPluginMetadata.Disabled)
            {
                LoadThemes();
            }
        }

        private void LoadThemes() 
            => themes = ThemeManager.Instance.LoadAvailableThemes().Select(Path.GetFileNameWithoutExtension);

        private static Result CreateThemeResult(string theme) => CreateThemeResult(theme, 0, null);

        private static Result CreateThemeResult(string theme, int score, IList<int> highlightData)
        {
            string title;
            if (theme == ThemeManager.Instance.Settings.Theme)
            {
                title = $"{theme} ★";
                score = 2000;
            }
            else
            {
                title = theme;
            }

            return new Result
            {
                Title = title,
                TitleHighlightData = highlightData,
                Glyph = new GlyphInfo("/Resources/#Segoe Fluent Icons", "\ue790"),
                Score = score,
                Action = c =>
                {
                    ThemeManager.Instance.ChangeTheme(theme);
                    return true;
                }
            };
        }

        public void Dispose()
        {
            if (context != null && context.API != null)
            {
                context.API.VisibilityChanged -= OnVisibilityChanged;
            }
        }
    }
}
