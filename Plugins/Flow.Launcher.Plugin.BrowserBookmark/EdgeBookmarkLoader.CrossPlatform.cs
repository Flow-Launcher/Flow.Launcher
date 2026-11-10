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
        if (OperatingSystem.IsLinux())
        {
            return LoadLinuxEdgeBookmarks();
        }

        var bookmarks = new List<Bookmark>();
        var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge"), "Microsoft Edge"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge Beta"), "Microsoft Edge Beta"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge Dev"), "Microsoft Edge Dev"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Microsoft Edge Canary"), "Microsoft Edge Canary"));

        return bookmarks;
    }

    /// <summary>
    /// Linux profile roots in $XDG_CONFIG_HOME (default ~/.config; .NET's ApplicationData honours the variable),
    /// plus the Flatpak build's private config folder.
    /// </summary>
    private List<Bookmark> LoadLinuxEdgeBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        var configPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var flatpakPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".var", "app");
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "microsoft-edge"), "Microsoft Edge"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "microsoft-edge-beta"), "Microsoft Edge Beta"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "microsoft-edge-dev"), "Microsoft Edge Dev"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(flatpakPath, "com.microsoft.Edge", "config", "microsoft-edge"), "Microsoft Edge"));

        return bookmarks;
    }
}
