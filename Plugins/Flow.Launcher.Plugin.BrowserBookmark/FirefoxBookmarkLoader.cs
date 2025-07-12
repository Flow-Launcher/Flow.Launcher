using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        // DO NOT watch Firefox files, as places.sqlite is updated on every navigation,
        // which would cause constant, performance-killing reloads.
        // A periodic check on query is used instead.

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

            // Put results in list
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
        if (!File.Exists(dbPath)) return;

        FaviconHelper.ExecuteWithTempDb(_faviconCacheDir, dbPath, tempDbPath =>
        {
            var savedPaths = new ConcurrentDictionary<string, bool>();

            // Get favicons based on bookmarks concurrently
            Parallel.ForEach(bookmarks, bookmark =>
            {
                if (string.IsNullOrEmpty(bookmark.Url) || !Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var uri))
                    return;

                using var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly;Pooling=false");
                connection.Open();

                try
                {
                    // Query for latest Firefox version favicon structure
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT i.id, i.data
                        FROM moz_icons i
                        JOIN moz_icons_to_pages ip ON i.id = ip.icon_id
                        JOIN moz_pages_w_icons p ON ip.page_id = p.id
                        WHERE p.page_url GLOB @pattern
                        AND i.data IS NOT NULL
                        ORDER BY i.width DESC
                        LIMIT 1";

                    cmd.Parameters.AddWithValue("@pattern", $"http*{uri.Host}/*");

                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return;

                    var id = reader.GetInt64(0).ToString();
                    var imageData = (byte[])reader["data"];

                    if (imageData is not { Length: > 0 })
                        return;

                    var faviconPath = Path.Combine(_faviconCacheDir, $"firefox_{uri.Host}_{id}.png");

                    if (savedPaths.TryAdd(faviconPath, true))
                    {
                        if (FaviconHelper.SaveBitmapData(imageData, faviconPath))
                            bookmark.FaviconPath = faviconPath;
                    }
                    else
                    {
                        bookmark.FaviconPath = faviconPath;
                    }
                }
                catch (Exception ex)
                {
                    Main._context.API.LogException(ClassName, $"Failed to extract favicon for: {bookmark.Url}", ex);
                }
                finally
                {
                    // Cache connection and clear pool after all operations to avoid issue:
                    // ObjectDisposedException: Safe handle has been closed.
                    SqliteConnection.ClearPool(connection);
                    connection.Close();
                }
            });
        });
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

    /*
        Current profiles.ini structure example as of Firefox version 69.0.1

        [Install736426B0AF4A39CB]
        Default=Profiles/7789f565.default-release   <== this is the default profile this plugin will get the bookmarks from. When opened Firefox will load the default profile
        Locked=1

        [Profile2]
        Name=dummyprofile
        IsRelative=0
        Path=C:\t6h2yuq8.dummyprofile  <== Note this is a custom location path for the profile user can set, we need to cater for this in code.

        [Profile1]
        Name=default
        IsRelative=1
        Path=Profiles/cydum7q4.default
        Default=1

        [Profile0]
        Name=default-release
        IsRelative=1
        Path=Profiles/7789f565.default-release

        [General]
        StartWithLastProfile=1
        Version=2
    */
    private static string GetProfileIniPath(string profileFolderPath)
    {
        var profileIni = Path.Combine(profileFolderPath, @"profiles.ini");
        if (!File.Exists(profileIni))
            return string.Empty;

        try
        {
            // Parse the ini file into a dictionary of sections for easier and more reliable access.
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

            // STRATEGY 1 (Primary): Find the default profile using the 'Default' key in the [Install] or [General] sections.
            // This is the most reliable method for modern Firefox versions.
            string defaultPathRaw = null;
            var installSection = profiles.FirstOrDefault(p => p.Key.StartsWith("Install"));
            // Fallback to the [General] section if the [Install] section is not found.
            (installSection.Value ?? profiles.GetValueOrDefault("General"))?.TryGetValue("Default", out defaultPathRaw);

            if (!string.IsNullOrEmpty(defaultPathRaw))
            {
                // The value of 'Default' is the path itself. We now find the profile section that has this path.
                profileSection = profiles.Values.FirstOrDefault(v => v.TryGetValue("Path", out var path) && path == defaultPathRaw);
            }

            // STRATEGY 2 (Fallback): If the primary strategy fails, look for a profile with the 'Default=1' flag.
            // This is for older versions or non-standard configurations.
            if (profileSection == null)
            {
                profileSection = profiles.Values.FirstOrDefault(section => section.TryGetValue("Default", out var value) && value == "1");
            }

            // If no profile section was found by either strategy, we cannot proceed.
            if (profileSection == null)
                return string.Empty;

            // We have the correct profile section, now resolve its path.
            if (!profileSection.TryGetValue("Path", out var pathValue) || string.IsNullOrEmpty(pathValue))
                return string.Empty;

            // Check if the path is relative or absolute. It defaults to relative if 'IsRelative' is not "0".
            profileSection.TryGetValue("IsRelative", out var isRelativeRaw);

            // The path in the ini file often uses forward slashes, so normalize them.
            var profilePath = isRelativeRaw != "0"
                ? Path.Combine(profileFolderPath, pathValue.Replace('/', Path.DirectorySeparatorChar))
                : pathValue; // If IsRelative is "0", the path is absolute and used as-is.

            // Path.GetFullPath will resolve any relative parts (like "..") and give us a clean, absolute path.
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
