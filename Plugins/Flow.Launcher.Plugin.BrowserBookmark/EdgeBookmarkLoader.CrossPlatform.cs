using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class EdgeBookmarkLoader
{
    // macOS profile roots; LocalApplicationData is ~/Library/Application Support there.
    private partial List<Bookmark> LoadEdgeBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge"), "Microsoft Edge"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge Beta"), "Microsoft Edge Beta"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge Dev"), "Microsoft Edge Dev"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge Canary"), "Microsoft Edge Canary"));

        return bookmarks;
    }
}
