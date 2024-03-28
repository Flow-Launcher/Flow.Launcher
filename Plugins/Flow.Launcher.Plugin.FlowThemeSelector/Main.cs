using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Plugin.FlowThemeSelector
{
    public class FlowThemeSelector : IPlugin, IReloadable, IDisposable
    {
        private PluginInitContext context;
        private IEnumerable<string> themes;

        public void Init(PluginInitContext context)
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

            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return themes.Select(CreateThemeResult)
                             .OrderBy(x => x.Title)
                             .ToList();
            }

            return themes.Select(theme => (theme, matchResult: context.API.FuzzySearch(query.Search, theme)))
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

        public void LoadThemes() => themes = ThemeManager.Instance.LoadAvailableThemes().Select(Path.GetFileNameWithoutExtension);

        public static Result CreateThemeResult(string theme) => CreateThemeResult(theme, 0, null);

        public static Result CreateThemeResult(string theme, int score, IList<int> highlightData)
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

        public void ReloadData() => LoadThemes();

        public void Dispose()
        {
            if (context != null && context.API != null)
            {
                context.API.VisibilityChanged -= OnVisibilityChanged;
            }
        }

    }
}
