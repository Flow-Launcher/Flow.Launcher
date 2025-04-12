using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public class ChromeBookmarkLoader : ChromiumBookmarkLoader
{
    public override List<Bookmark> GetBookmarks()
    {
        return LoadChromeBookmarks();
    }

    private List<Bookmark> LoadChromeBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, @"Google\Chrome\User Data"), "Google Chrome"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, @"Google\Chrome SxS\User Data"), "Google Chrome Canary"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, @"Chromium\User Data"), "Chromium"));
        return bookmarks;
    }
}
