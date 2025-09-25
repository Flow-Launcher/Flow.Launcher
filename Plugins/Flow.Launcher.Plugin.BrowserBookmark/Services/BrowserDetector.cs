#nullable enable
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public static class BrowserDetector
{
    public static IEnumerable<string> GetChromiumProfileDirectories(string basePath)
    {
        if (!Directory.Exists(basePath))
            return Enumerable.Empty<string>();

        var profileDirs = Directory.EnumerateDirectories(basePath, "Profile *").ToList();

        var defaultProfile = Path.Combine(basePath, "Default");
        if (Directory.Exists(defaultProfile))
            profileDirs.Add(defaultProfile);

        // Also check the base path itself, as some browsers use it as the profile directory,
        // or the user might provide a direct path to a profile.
        profileDirs.Add(basePath);

        return profileDirs.Distinct();
    }

    public static BrowserType DetectBrowserType(string dataDirectoryPath)
    {
        if (string.IsNullOrEmpty(dataDirectoryPath) || !Directory.Exists(dataDirectoryPath))
            return BrowserType.Unknown;

        // Check for Chromium-based browsers by looking for the 'Bookmarks' file.
        var profileDirectories = GetChromiumProfileDirectories(dataDirectoryPath);
        if (profileDirectories.Any(p => File.Exists(Path.Combine(p, "Bookmarks"))))
        {
            return BrowserType.Chromium;
        }

        // Check for Firefox-based browsers by looking for 'places.sqlite'.
        // This leverages the existing FirefoxProfileFinder logic.
        if (File.Exists(Path.Combine(dataDirectoryPath, "places.sqlite")) || !string.IsNullOrEmpty(FirefoxProfileFinder.GetPlacesPathFromProfileDir(dataDirectoryPath)))
        {
            return BrowserType.Firefox;
        }

        return BrowserType.Unknown;
    }
}
