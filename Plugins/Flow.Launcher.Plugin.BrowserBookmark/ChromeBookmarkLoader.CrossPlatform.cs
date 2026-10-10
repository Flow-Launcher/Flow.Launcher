using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class ChromeBookmarkLoader
{
    // macOS profile roots; LocalApplicationData is ~/Library/Application Support there.
    // Other Unix layouts (e.g. ~/.config/google-chrome on Linux) are not probed.
    private partial List<Bookmark> LoadChromeBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Google", "Chrome"), "Google Chrome"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Google", "Chrome Canary"), "Google Chrome Canary"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Chromium"), "Chromium"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "BraveSoftware", "Brave-Browser"), "Brave"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Vivaldi"), "Vivaldi"));
        return bookmarks;
    }
}
