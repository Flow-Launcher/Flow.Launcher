using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.BrowserBookmark.Helper;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Data.Sqlite;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public abstract class FirefoxBookmarkLoaderBase : IBookmarkLoader
{
    private static readonly string ClassName = nameof(FirefoxBookmarkLoaderBase);

    private readonly string _faviconCacheDir;

    protected FirefoxBookmarkLoaderBase()
    {
        _faviconCacheDir = Main._faviconCacheDir;
    }

    public abstract List<Bookmark> GetBookmarks();

    // Updated query - removed favicon_id column
    private const string QueryAllBookmarks = """
        SELECT moz_places.url, moz_bookmarks.title
        FROM moz_places
            INNER JOIN moz_bookmarks ON (
                moz_bookmarks.fk NOT NULL AND moz_bookmarks.title NOT NULL AND moz_bookmarks.fk = moz_places.id
            )
        ORDER BY moz_places.visit_count DESC
        """;

    protected List<Bookmark> GetBookmarksFromPath(string placesPath)
    {
        // Variable to store bookmark list
        var bookmarks = new List<Bookmark>();

        // Return empty list if places.sqlite file doesn't exist
        if (string.IsNullOrEmpty(placesPath) || !File.Exists(placesPath))
            return bookmarks;

        // Try to register file monitoring
        try
        {
            Main.RegisterBookmarkFile(placesPath);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to register Firefox bookmark file monitoring: {placesPath}", ex);
            return bookmarks;
        }

        var tempDbPath = Path.Combine(_faviconCacheDir, $"tempplaces_{Guid.NewGuid()}.sqlite");

        try
        {
            // Use a copy to avoid lock issues with the original file
            File.Copy(placesPath, tempDbPath, true);

            // Create the connection string and init the connection
            using var dbConnection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly");

            // Open connection to the database file and execute the query
            dbConnection.Open();
            using var command = new SqliteCommand(QueryAllBookmarks, dbConnection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                bookmarks.Add(new Bookmark(
                    reader["title"] is DBNull ? string.Empty : reader["title"].ToString(),
                    reader["url"].ToString(),
                    "Firefox"
                ));
            }


            // Load favicons after loading bookmarks
            if (Main._settings.EnableFavicons)
            {
                var faviconDbPath = Path.Combine(Path.GetDirectoryName(placesPath), "favicons.sqlite");
                if (File.Exists(faviconDbPath))
                {
                    Main._context.API.StopwatchLogInfo(ClassName, $"Load {bookmarks.Count} favicons cost", () =>
                    {
                        LoadFaviconsFromDb(faviconDbPath, bookmarks);
                    });
                }
            }

            // Close the connection so that we can delete the temporary file
            // https://github.com/dotnet/efcore/issues/26580
            SqliteConnection.ClearPool(dbConnection);
            dbConnection.Close();
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to load Firefox bookmarks: {placesPath}", ex);
        }

        // Delete temporary file
        try
        {
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex);
        }

        return bookmarks;
    }

    private void LoadFaviconsFromDb(string dbPath, List<Bookmark> bookmarks)
    {
        const string sql = @"
        SELECT i.id, i.data
        FROM moz_icons i
        JOIN moz_icons_to_pages ip ON i.id = ip.icon_id
        JOIN moz_pages_w_icons p ON ip.page_id = p.id
        WHERE p.page_url GLOB @pattern
        AND i.data IS NOT NULL
        ORDER BY i.width DESC
        LIMIT 1";

        FaviconHelper.ProcessFavicons(
            dbPath,
            _faviconCacheDir,
            bookmarks,
            sql,
            "http*",
            reader => (reader.GetInt64(0).ToString(), (byte[])reader["data"]),
            // Always generate a .png path. The helper will handle the conversion.
            (uri, id, data) => Path.Combine(_faviconCacheDir, $"firefox_{uri.Host}_{id}.png")
        );
    }
}

public class FirefoxBookmarkLoader : FirefoxBookmarkLoaderBase
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

    private static string GetProfileIniPath(string profileFolderPath)
    {
        var profileIni = Path.Combine(profileFolderPath, @"profiles.ini");
        if (!File.Exists(profileIni))
            return string.Empty;

        try
        {
            // Parse the ini file into a dictionary of sections
            var profiles = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> currentSection = null;
            foreach (var line in File.ReadLines(profileIni))
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    profiles[sectionName] = currentSection;
                }
                else if (currentSection != null && trimmedLine.Contains('='))
                {
                    var parts = trimmedLine.Split('=', 2);
                    currentSection[parts[0]] = parts[1];
                }
            }

            Dictionary<string, string> profileSection = null;

            // Strategy 1: Find the profile with Default=1
            profileSection = profiles.Values.FirstOrDefault(section => section.TryGetValue("Default", out var value) && value == "1");

            // Strategy 2: If no profile has Default=1, use the Default key from the [Install] or [General] section
            if (profileSection == null)
            {
                string defaultPathRaw = null;
                var installSection = profiles.FirstOrDefault(p => p.Key.StartsWith("Install"));
                // Fallback to General section if Install section not found
                (installSection.Value ?? profiles.GetValueOrDefault("General"))?.TryGetValue("Default", out defaultPathRaw);

                if (!string.IsNullOrEmpty(defaultPathRaw))
                {
                    // The value of 'Default' is the path, find the corresponding profile section
                    profileSection = profiles.Values.FirstOrDefault(v => v.TryGetValue("Path", out var path) && path == defaultPathRaw);
                }
            }

            if (profileSection == null)
                return string.Empty;

            // We have the profile section, now resolve the path
            if (!profileSection.TryGetValue("Path", out var pathValue) || string.IsNullOrEmpty(pathValue))
                return string.Empty;

            profileSection.TryGetValue("IsRelative", out var isRelativeRaw);

            // If IsRelative is "1" or not present (defaults to relative), combine with profileFolderPath.
            // The path in the ini file often uses forward slashes, so normalize them.
            var profilePath = isRelativeRaw != "0"
                ? Path.Combine(profileFolderPath, pathValue.Replace('/', Path.DirectorySeparatorChar))
                : pathValue;

            // Path.GetFullPath will resolve any relative parts and give us a clean absolute path.
            var fullProfilePath = Path.GetFullPath(profilePath);

            var placesPath = Path.Combine(fullProfilePath, "places.sqlite");

            return File.Exists(placesPath) ? placesPath : string.Empty;
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(nameof(FirefoxBookmarkLoader), $"Failed to parse {profileIni}", ex);
            return string.Empty;
        }
    }
}
