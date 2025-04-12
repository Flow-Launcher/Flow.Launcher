using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public class CustomChromiumBookmarkLoader : ChromiumBookmarkLoader
{
    public CustomChromiumBookmarkLoader(CustomBrowser browser)
    {
        BrowserName = browser.Name;
        BrowserDataPath = browser.DataDirectoryPath;
    }
    public string BrowserDataPath { get; init; }
    public string BookmarkFilePath { get; init; }
    public string BrowserName { get; init; }

    public override List<Bookmark> GetBookmarks() => BrowserDataPath != null ? LoadBookmarks(BrowserDataPath, BrowserName) : LoadBookmarksFromFile(BookmarkFilePath, BrowserName);
}
