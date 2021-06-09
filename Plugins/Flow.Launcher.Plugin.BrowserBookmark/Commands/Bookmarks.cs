using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.BrowserBookmark.Commands
{
    internal static class Bookmarks
    {
        internal static MatchResult MatchProgram(Bookmark bookmark, string queryString)
        {
            var match = Main.context.API.FuzzySearch(queryString, bookmark.Name);
            if (match.IsSearchPrecisionScoreMet())
                return match;

            return Main.context.API.FuzzySearch(queryString, bookmark.Url);
        }

        internal static List<Bookmark> LoadAllBookmarks()
        {
            var allbookmarks = new List<Bookmark>();

            var chromeBookmarks = new ChromeBookmarks();
            var mozBookmarks = new FirefoxBookmarks();
            var edgeBookmarks = new EdgeBookmarks();

            //TODO: Let the user select which browser's bookmarks are displayed
            // Add Firefox bookmarks
            mozBookmarks.GetBookmarks().ForEach(x => allbookmarks.Add(x));

            // Add Chrome bookmarks
            chromeBookmarks.GetBookmarks().ForEach(x => allbookmarks.Add(x));

            // Add Edge (Chromium) bookmarks
            edgeBookmarks.GetBookmarks().ForEach(x => allbookmarks.Add(x));

            return allbookmarks.Distinct().ToList();
        }
    }
}
