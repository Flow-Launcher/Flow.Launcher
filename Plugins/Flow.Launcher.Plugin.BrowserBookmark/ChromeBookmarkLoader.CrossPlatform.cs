using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class ChromeBookmarkLoader
{
    // macOS profile roots; LocalApplicationData is ~/Library/Application Support there.
    // On Linux the profile roots live in $XDG_CONFIG_HOME (ApplicationData), see LoadLinuxChromeBookmarks.
    private partial List<Bookmark> LoadChromeBookmarks()
    {
        if (OperatingSystem.IsLinux())
        {
            return LoadLinuxChromeBookmarks();
        }

        var bookmarks = new List<Bookmark>();
        var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Google", "Chrome"), "Google Chrome"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Google", "Chrome Canary"), "Google Chrome Canary"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Chromium"), "Chromium"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "BraveSoftware", "Brave-Browser"), "Brave"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(platformPath, "Vivaldi"), "Vivaldi"));
        return bookmarks;
    }

    /// <summary>
    /// Chromium browsers on Linux keep their profiles in $XDG_CONFIG_HOME (default ~/.config; .NET's ApplicationData
    /// honours the variable). Flatpak builds use the app's private ~/.var/app/{id}/config and the Chromium snap
    /// ~/snap/chromium/common. Opera is not listed: it keeps Bookmarks in the root folder instead of per-profile folders.
    /// </summary>
    private List<Bookmark> LoadLinuxChromeBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        var configPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var flatpakPath = Path.Combine(home, ".var", "app");

        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "google-chrome"), "Google Chrome"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "google-chrome-beta"), "Google Chrome Beta"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "google-chrome-unstable"), "Google Chrome Dev"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "google-chrome-canary"), "Google Chrome Canary"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(flatpakPath, "com.google.Chrome", "config", "google-chrome"), "Google Chrome"));

        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "chromium"), "Chromium"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(flatpakPath, "org.chromium.Chromium", "config", "chromium"), "Chromium"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(home, "snap", "chromium", "common", "chromium"), "Chromium"));

        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "BraveSoftware", "Brave-Browser"), "Brave"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "BraveSoftware", "Brave-Browser-Beta"), "Brave Beta"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "BraveSoftware", "Brave-Browser-Nightly"), "Brave Nightly"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(flatpakPath, "com.brave.Browser", "config", "BraveSoftware", "Brave-Browser"), "Brave"));

        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "vivaldi"), "Vivaldi"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(configPath, "vivaldi-snapshot"), "Vivaldi Snapshot"));
        bookmarks.AddRange(LoadBookmarks(Path.Combine(flatpakPath, "com.vivaldi.Vivaldi", "config", "vivaldi"), "Vivaldi"));
        return bookmarks;
    }
}
