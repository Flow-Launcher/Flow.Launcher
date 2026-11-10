using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class FirefoxBookmarkLoader
{
    /// <summary>
    /// Searches the places.sqlite db of the default profile and returns all bookmarks
    /// </summary>
    public override List<Bookmark> GetBookmarks()
    {
        if (OperatingSystem.IsLinux())
        {
            var bookmarks = new List<Bookmark>();
            foreach (var profileFolderPath in LinuxProfileFolderPaths)
            {
                bookmarks.AddRange(GetBookmarksFromPath(GetLinuxPlacesPath(profileFolderPath)));
            }
            return bookmarks;
        }

        return GetBookmarksFromPath(PlacesPath);
    }

    /// <summary>
    /// Path to places.sqlite of the default profile on macOS.
    /// E.g. /Users/{UserName}/Library/Application Support/Firefox (ApplicationData resolves there on macOS).
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

    /// <summary>
    /// Folders holding profiles.ini on Linux: the classic ~/.mozilla/firefox, $XDG_CONFIG_HOME/mozilla/firefox used by
    /// new installs since Firefox 147 (ApplicationData honours $XDG_CONFIG_HOME), and the Flatpak and Snap sandboxes.
    /// </summary>
    private static IEnumerable<string> LinuxProfileFolderPaths
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var flatpakPath = Path.Combine(home, ".var", "app", "org.mozilla.firefox");
            return new[]
                {
                    Path.Combine(home, ".mozilla", "firefox"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mozilla", "firefox"),
                    Path.Combine(flatpakPath, ".mozilla", "firefox"),
                    Path.Combine(flatpakPath, "config", "mozilla", "firefox"),
                    Path.Combine(home, "snap", "firefox", "common", ".mozilla", "firefox")
                }
                .Distinct();
        }
    }

    /// <summary>
    /// The install's default profile (see <see cref="GetProfileIniPath"/>). profiles.ini files without [Install…]
    /// sections, as written by Home Manager or builds with legacy profiles, only mark a [Profile…] with Default=1;
    /// that profile, or the only profile, is used then.
    /// </summary>
    private static string GetLinuxPlacesPath(string profileFolderPath)
    {
        var placesPath = GetProfileIniPath(profileFolderPath);
        if (File.Exists(placesPath) || !File.Exists(Path.Combine(profileFolderPath, "profiles.ini")))
        {
            return placesPath;
        }

        try
        {
            var profiles = ReadProfileSections(Path.Combine(profileFolderPath, "profiles.ini"));
            var profile = profiles.Find(p => p.GetValueOrDefault("Default") == "1")
                ?? (profiles.Count == 1 ? profiles[0] : null);
            if (profile is null || !profile.TryGetValue("Path", out var path) || string.IsNullOrEmpty(path))
            {
                return placesPath;
            }

            var profilePath = profile.GetValueOrDefault("IsRelative") == "0"
                ? path
                : Path.Combine(profileFolderPath, path);
            return Path.Combine(profilePath, "places.sqlite");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Main.Context.API.LogException(nameof(FirefoxBookmarkLoader), $"Failed to read profiles.ini in <{profileFolderPath}>", e);
            return placesPath;
        }
    }

    /// <summary>
    /// Key/value pairs of each [Profile…] section in profiles.ini, in file order.
    /// </summary>
    private static List<Dictionary<string, string>> ReadProfileSections(string profileIni)
    {
        var profiles = new List<Dictionary<string, string>>();
        Dictionary<string, string> current = null;
        foreach (var rawLine in File.ReadLines(profileIni))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line.StartsWith("[Profile", StringComparison.OrdinalIgnoreCase) ? [] : null;
                if (current is not null)
                {
                    profiles.Add(current);
                }
                continue;
            }

            var separator = line.IndexOf('=');
            if (current is not null && separator > 0)
            {
                current[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return profiles;
    }
}
