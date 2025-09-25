#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

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

            // Priority 1: Check for an [Install...] section which often contains the default profile path.
            var installMatch = Regex.Match(iniContent, @"^\[Install[^\]]+\](?:.|\n|\r)*?^Default\s*=\s*(.+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (installMatch.Success)
            {
                var path = installMatch.Groups[1].Value.Trim();
                // This path is typically relative, e.g., "Profiles/xyz.default-release"
                var profilePath = Path.Combine(profileFolderPath, path.Replace('/', Path.DirectorySeparatorChar));
                var placesDb = Path.Combine(profilePath, "places.sqlite");
                if (File.Exists(placesDb))
                {
                    return placesDb;
                }
            }

            // Priority 2: Parse individual [Profile...] sections.
            var profileSections = Regex.Matches(iniContent, @"^\[Profile[^\]]+\](?:.|\n|\r)*?(?=\r?\n\[|$)", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Value)
                .ToArray();

            string? targetSection = null;

            // Find a profile named "default-release" that is also marked as default.
            targetSection = profileSections.FirstOrDefault(s =>
                Regex.IsMatch(s, @"^Name\s*=\s*default-release", RegexOptions.Multiline | RegexOptions.IgnoreCase) &&
                Regex.IsMatch(s, @"^Default\s*=\s*1", RegexOptions.Multiline | RegexOptions.IgnoreCase));
            
            // Find any profile marked as default.
            if (targetSection == null)
            {
                targetSection = profileSections.FirstOrDefault(s => Regex.IsMatch(s, @"^Default\s*=\s*1", RegexOptions.Multiline | RegexOptions.IgnoreCase));
            }

            // Fallback to the first profile in the file if no default is marked.
            if (targetSection == null)
            {
                targetSection = profileSections.FirstOrDefault();
            }
            
            if (targetSection == null) 
                return null;
            
            var pathMatch = Regex.Match(targetSection, @"^Path\s*=\s*(.+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!pathMatch.Success) 
                return null;

            var pathValue = pathMatch.Groups[1].Value.Trim();
            
            // IsRelative=0 means it's an absolute path. The default is relative (IsRelative=1).
            var isRelative = !Regex.IsMatch(targetSection, @"^IsRelative\s*=\s*0", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            
            var finalProfilePath = isRelative ? Path.Combine(profileFolderPath, pathValue.Replace('/', Path.DirectorySeparatorChar)) : pathValue;
            var finalPlacesDb = Path.Combine(finalProfilePath, "places.sqlite");

            return File.Exists(finalPlacesDb) ? finalPlacesDb : null;
        }
        catch
        {
            // Logged in the calling service
            return null;
        }
    }
}
