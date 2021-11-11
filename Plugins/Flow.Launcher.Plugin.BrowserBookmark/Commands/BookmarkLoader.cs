using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.BrowserBookmark.Commands
{
    internal static class BookmarkLoader
    {
        public static IPublicAPI publicAPI { get; internal set; }

        internal static MatchResult MatchProgram(Bookmark bookmark, string queryString)
        {
            var match = publicAPI.FuzzySearch(queryString, bookmark.Name);
            if (match.IsSearchPrecisionScoreMet())
                return match;

            return publicAPI.FuzzySearch(queryString, bookmark.Url);
        }

        internal static List<Bookmark> LoadAllBookmarks(Settings setting)
        {

            var chromeBookmarks = new ChromeBookmarkLoader();
            var mozBookmarks = new FirefoxBookmarkLoader();
            var edgeBookmarks = new EdgeBookmarkLoader();

            var allBookmarks = new List<Bookmark>();

            // Add Firefox bookmarks
            allBookmarks.AddRange(mozBookmarks.GetBookmarks());

            // Add Chrome bookmarks
            allBookmarks.AddRange(chromeBookmarks.GetBookmarks());

            // Add Edge (Chromium) bookmarks
            allBookmarks.AddRange(edgeBookmarks.GetBookmarks());

            foreach (var browser in setting.CustomChromiumBrowsers)
            {
                var loader = new CustomChromiumBookmarkLoader(browser);
                allBookmarks.AddRange(loader.GetBookmarks());
            }

            return allBookmarks.Distinct().ToList();
        }
    }
}