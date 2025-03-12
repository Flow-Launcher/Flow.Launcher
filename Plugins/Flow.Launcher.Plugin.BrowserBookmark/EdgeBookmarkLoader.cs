using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public class EdgeBookmarkLoader : ChromiumBookmarkLoader
{
    private List<Bookmark> LoadEdgeBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, @"Microsoft\Edge\User Data"), "Microsoft Edge"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, @"Microsoft\Edge Dev\User Data"), "Microsoft Edge Dev"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, @"Microsoft\Edge SxS\User Data"), "Microsoft Edge Canary"));

        return bookmarks;
    }

    public override List<Bookmark> GetBookmarks() => LoadEdgeBookmarks();
}
