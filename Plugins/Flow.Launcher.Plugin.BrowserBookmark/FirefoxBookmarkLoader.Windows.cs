using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class FirefoxBookmarkLoader
{
    /// <summary>
    /// Searches the places.sqlite db and returns all bookmarks
    /// </summary>
    public override List<Bookmark> GetBookmarks()
    {
        var bookmarks = new List<Bookmark>();
        bookmarks.AddRange(GetBookmarksFromPath(PlacesPath));
        bookmarks.AddRange(GetBookmarksFromPath(MsixPlacesPath));
        return bookmarks;
    }

    /// <summary>
    /// Path to places.sqlite of Msi installer
    /// E.g. C:\Users\{UserName}\AppData\Roaming\Mozilla\Firefox
    /// <see href="https://support.mozilla.org/en-US/kb/profiles-where-firefox-stores-user-data#w_finding-your-profile-without-opening-firefox"/>
    /// </summary>
    private static string PlacesPath
    {
        get
        {
            var profileFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox");
            return GetProfileIniPath(profileFolderPath);
        }
    }

    /// <summary>
    /// Path to places.sqlite of MSIX installer
    /// E.g. C:\Users\{UserName}\AppData\Local\Packages\Mozilla.Firefox_n80bbvh6b1yt2\LocalCache\Roaming\Mozilla\Firefox
    /// <see href="https://support.mozilla.org/en-US/kb/profiles-where-firefox-stores-user-data#w_finding-your-profile-without-opening-firefox"/>
    /// </summary>
    public static string MsixPlacesPath
    {
        get
        {
            var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var packagesPath = Path.Combine(platformPath, "Packages");
            try
            {
                // Search for folder with Mozilla.Firefox prefix
                var firefoxPackageFolder = Directory.EnumerateDirectories(packagesPath, "Mozilla.Firefox*",
                    SearchOption.TopDirectoryOnly).FirstOrDefault();

                // Msix FireFox not installed
                if (firefoxPackageFolder == null) return string.Empty;

                var profileFolderPath = Path.Combine(firefoxPackageFolder, @"LocalCache\Roaming\Mozilla\Firefox");
                return GetProfileIniPath(profileFolderPath);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
