using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Plugin.Sys
{
    public class ThemeSelector : IDisposable
    {
        public const string Keyword = "fltheme";

        private readonly Theme _theme;
        private readonly PluginInitContext _context;

        private IEnumerable<string> themes;

        public ThemeSelector(PluginInitContext context)
        {
            _context = context;
            _theme = Ioc.Default.GetRequiredService<Theme>();
            context.API.VisibilityChanged += OnVisibilityChanged;
        }

        ~ThemeSelector()
        {
            Dispose(false);
        }

        public List<Result> Query(Query query)
        {
            if (query.IsReQuery)
            {
                LoadThemes();
            }

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

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs args)
        {
            if (args.IsVisible && !_context.CurrentPluginMetadata.Disabled)
            {
                LoadThemes();
            }
        }

        private void LoadThemes() 
            => themes = _theme.LoadAvailableThemes().Select(x => x.FileNameWithoutExtension);

        private Result CreateThemeResult(string theme) => CreateThemeResult(theme, 0, null);

        private Result CreateThemeResult(string theme, int score, IList<int> highlightData)
        {
            string title;
            if (theme == _theme.CurrentTheme)
            {
                title = $"{theme} ★";
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
                Glyph = new GlyphInfo("/Resources/#Segoe Fluent Icons", "\ue790"),
                Score = score,
                Action = c =>
                {
                    _theme.ChangeTheme(theme);
                    return true;
                }
            };
        }

        private bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_context?.API != null)
                    {
                        _context.API.VisibilityChanged -= OnVisibilityChanged;
                    }
                }
                // Free unmanaged resources
                disposed = true;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
