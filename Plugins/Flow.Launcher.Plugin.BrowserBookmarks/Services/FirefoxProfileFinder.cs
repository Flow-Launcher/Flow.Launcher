#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public static class FirefoxProfileFinder
{
    public static string? GetFirefoxPlacesPath()
    {
        // Standard MSI installer path
        var standardPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox");
        var placesPath = GetPlacesPathFromProfileDir(standardPath);
        
        return !string.IsNullOrEmpty(placesPath) ? placesPath : null;
    }

    public static string? GetFirefoxMsixPlacesPath()
    {
        var packagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (!Directory.Exists(packagesPath)) 
            return null;

        try
        {
            var firefoxPackageFolder = Directory.EnumerateDirectories(packagesPath, "Mozilla.Firefox*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (firefoxPackageFolder == null) 
                return null;

            var profileFolderPath = Path.Combine(firefoxPackageFolder, @"LocalCache\Roaming\Mozilla\Firefox");
            return GetPlacesPathFromProfileDir(profileFolderPath);
        }
        catch
        {
            // Logged in the calling service
            return null;
        }
    }

    public static string? GetPlacesPathFromProfileDir(string profileFolderPath)
    {
        var profileIni = Path.Combine(profileFolderPath, @"profiles.ini");
        if (!File.Exists(profileIni))
            return null;

        try
        {
            var iniContent = File.ReadAllText(profileIni);
            // Try to find the default-release profile first, which is the most common case.
            var profileSectionMatch = Regex.Match(iniContent, @"\[Profile[^\]]+\]\s*Name=default-release[\s\S]+?Path=([^\r\n]+)[\s\S]+?Default=1", RegexOptions.IgnoreCase);
            
            // Fallback to any default profile.
            if (!profileSectionMatch.Success)
            {
                profileSectionMatch = Regex.Match(iniContent, @"\[Profile[^\]]+\][\s\S]+?Path=([^\r\n]+)[\s\S]+?Default=1", RegexOptions.IgnoreCase);
            }

            // Fallback to the first available profile if no default is marked.
            if (!profileSectionMatch.Success)
            {
                profileSectionMatch = Regex.Match(iniContent, @"\[Profile[^\]]+\][\s\S]+?Path=([^\r\n]+)");
            }

            if (!profileSectionMatch.Success) 
                return null;

            var path = profileSectionMatch.Groups[1].Value;
            var isRelative = !path.Contains(':');

            var profilePath = isRelative ? Path.Combine(profileFolderPath, path.Replace('/', Path.DirectorySeparatorChar)) : path;
            var placesDb = Path.Combine(profilePath, "places.sqlite");

            return File.Exists(placesDb) ? placesDb : null;
        }
        catch
        {
            // Logged in the calling service
            return null;
        }
    }
}
