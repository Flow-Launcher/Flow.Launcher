#nullable enable
using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public static class BrowserDetector
{
    public static BrowserType DetectBrowserType(string dataDirectoryPath)
    {
        if (string.IsNullOrEmpty(dataDirectoryPath) || !Directory.Exists(dataDirectoryPath))
            return BrowserType.Unknown;

        // Check for Chromium-based browsers by looking for the 'Bookmarks' file.
        // This includes checking common profile subdirectories.
        var profileDirectories = Directory.EnumerateDirectories(dataDirectoryPath, "Profile *").ToList();
        var defaultProfile = Path.Combine(dataDirectoryPath, "Default");
        if (Directory.Exists(defaultProfile))
            profileDirectories.Add(defaultProfile);
        
        // Also check the root directory itself, as some browsers use it directly.
        profileDirectories.Add(dataDirectoryPath);

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
