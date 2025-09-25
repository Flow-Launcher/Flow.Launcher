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
            
            var profileSections = Regex.Matches(iniContent, @"^\[Profile[^\]]+\](?:.|\n|\r)*?(?=\r?\n\[|$)", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Value)
                .ToArray();

            string? targetSection = null;

            // Priority 1: Find a profile named "default-release" that is also marked as default.
            targetSection = profileSections.FirstOrDefault(s => 
                Regex.IsMatch(s, @"\bName\s*=\s*default-release\b", RegexOptions.IgnoreCase) && 
                Regex.IsMatch(s, @"\bDefault\s*=\s*1\b", RegexOptions.IgnoreCase));
            
            // Priority 2: Find any profile marked as default.
            if (targetSection == null)
            {
                targetSection = profileSections.FirstOrDefault(s => Regex.IsMatch(s, @"\bDefault\s*=\s*1\b", RegexOptions.IgnoreCase));
            }

            // Priority 3: Fallback to the first profile in the file.
            if (targetSection == null)
            {
                targetSection = profileSections.FirstOrDefault();
            }
            
            if (targetSection == null) 
                return null;
            
            var pathMatch = Regex.Match(targetSection, @"^Path\s*=\s*(.+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!pathMatch.Success) 
                return null;

            var path = pathMatch.Groups[1].Value.Trim();
            
            // IsRelative=0 means it's an absolute path. The default is relative (IsRelative=1).
            var isRelative = !Regex.IsMatch(targetSection, @"^IsRelative\s*=\s*0", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            
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
