using System;
using System.Collections.Generic;
using System.IO;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class FirefoxBookmarkLoader
{
    /// <summary>
    /// Searches the places.sqlite db of the default profile and returns all bookmarks
    /// </summary>
    public override List<Bookmark> GetBookmarks()
    {
        return GetBookmarksFromPath(PlacesPath);
    }

    /// <summary>
    /// Path to places.sqlite of the default profile on macOS.
    /// E.g. /Users/{UserName}/Library/Application Support/Firefox (ApplicationData resolves there on macOS).
    /// Other Unix layouts (e.g. ~/.mozilla/firefox on Linux) are not probed.
    /// <see href="https://support.mozilla.org/en-US/kb/profiles-where-firefox-stores-user-data#w_finding-your-profile-without-opening-firefox"/>
    /// </summary>
    private static string PlacesPath
    {
        get
        {
            var profileFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Firefox");
            return GetProfileIniPath(profileFolderPath);
        }
    }
}
