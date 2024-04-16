using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.BrowserBookmark.Commands;

internal static class BookmarkLoader
{
    internal static MatchResult MatchProgram(Bookmark bookmark, string queryString)
    {
        var match = StringMatcher.FuzzySearch(queryString, bookmark.Name);
        if (match.IsSearchPrecisionScoreMet())
            return match;

        return StringMatcher.FuzzySearch(queryString, bookmark.Url);
    }

    internal static List<Bookmark> LoadAllBookmarks(Settings setting)
    {
        var allBookmarks = new List<Bookmark>();

        if (setting.LoadChromeBookmark)
        {
            // Add Chrome bookmarks
            var chromeBookmarks = new ChromeBookmarkLoader();
            allBookmarks.AddRange(chromeBookmarks.GetBookmarks());
        }

        if (setting.LoadFirefoxBookmark)
        {
            // Add Firefox bookmarks
            var mozBookmarks = new FirefoxBookmarkLoader();
            allBookmarks.AddRange(mozBookmarks.GetBookmarks());
        }

        if (setting.LoadEdgeBookmark)
        {
            // Add Edge (Chromium) bookmarks
            var edgeBookmarks = new EdgeBookmarkLoader();
            allBookmarks.AddRange(edgeBookmarks.GetBookmarks());
        }

        foreach (var browser in setting.CustomChromiumBrowsers)
        {
            IBookmarkLoader loader = browser.BrowserType switch
            {
                BrowserType.Chromium => new CustomChromiumBookmarkLoader(browser),
                BrowserType.Firefox => new CustomFirefoxBookmarkLoader(browser),
                _ => new CustomChromiumBookmarkLoader(browser),
            };
            allBookmarks.AddRange(loader.GetBookmarks());
        }

        return allBookmarks.Distinct().ToList();
    }
}
