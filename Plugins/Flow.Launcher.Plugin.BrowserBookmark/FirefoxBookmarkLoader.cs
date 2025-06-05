﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            var reader = new SqliteCommand(QueryAllBookmarks, dbConnection).ExecuteReader();

            // Get results in List<Bookmark> format
            bookmarks = reader
                .Select(
                    x => new Bookmark(
                        x["title"] is DBNull ? string.Empty : x["title"].ToString(),
                        x["url"].ToString(),
                        "Firefox"
                    )
                )
                .ToList();

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
        // Use a copy to avoid lock issues with the original file
        var tempDbPath = Path.Combine(_faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.sqlite");

        try
        {
            File.Copy(dbPath, tempDbPath, true);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
            }
            catch (Exception ex1)
            {
                Main._context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex1);
            }
            Main._context.API.LogException(ClassName, $"Failed to copy favicon DB: {dbPath}", ex);
            return;
        }

        try
        {
            // Since some bookmarks may have same favicon id, we need to record them to avoid duplicates
            var savedPaths = new ConcurrentDictionary<string, bool>();

            // Get favicons based on bookmarks concurrently
            Parallel.ForEach(bookmarks, bookmark =>
            {
                // Use read-only connection to avoid locking issues
                var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly");
                connection.Open();

                try
                {
                    if (string.IsNullOrEmpty(bookmark.Url))
                        return;

                    // Extract domain from URL
                    if (!Uri.TryCreate(bookmark.Url, UriKind.Absolute, out Uri uri))
                        return;

                    var domain = uri.Host;

                    // Query for latest Firefox version favicon structure
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT i.data
                        FROM moz_icons i
                        JOIN moz_icons_to_pages ip ON i.id = ip.icon_id
                        JOIN moz_pages_w_icons p ON ip.page_id = p.id
                        WHERE p.page_url LIKE @url
                        AND i.data IS NOT NULL
                        ORDER BY i.width DESC  -- Select largest icon available
                        LIMIT 1";

                    cmd.Parameters.AddWithValue("@url", $"%{domain}%");

                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read() || reader.IsDBNull(0))
                        return;

                    var imageData = (byte[])reader["data"];

                    if (imageData is not { Length: > 0 })
                        return;

                    string faviconPath;
                    if (IsSvgData(imageData))
                    {
                        faviconPath = Path.Combine(_faviconCacheDir, $"firefox_{domain}.svg");
                    }
                    else
                    {
                        faviconPath = Path.Combine(_faviconCacheDir, $"firefox_{domain}.png");
                    }

                    // Filter out duplicate favicons
                    if (savedPaths.TryAdd(faviconPath, true))
                    {
                        SaveBitmapData(imageData, faviconPath);
                    }

                    bookmark.FaviconPath = faviconPath;
                }
                catch (Exception ex)
                {
                    Main._context.API.LogException(ClassName, $"Failed to extract Firefox favicon: {bookmark.Url}", ex);
                }
                finally
                {
                    // https://github.com/dotnet/efcore/issues/26580
                    SqliteConnection.ClearPool(connection);
                    connection.Close();
                    connection.Dispose();
                }
            });
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to load Firefox favicon DB: {tempDbPath}", ex);
        }

        // Delete temporary file
        try
        {
            File.Delete(tempDbPath);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex);
        }
    }

    private static void SaveBitmapData(byte[] imageData, string outputPath)
    {
        try
        {
            File.WriteAllBytes(outputPath, imageData);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to save image: {outputPath}", ex);
        }
    }

    private static bool IsSvgData(byte[] data)
    {
        if (data.Length < 5)
            return false;
        string start = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(100, data.Length));
        return start.Contains("<svg") ||
               (start.StartsWith("<?xml") && start.Contains("<svg"));
    }
}

public class FirefoxBookmarkLoader : FirefoxBookmarkLoaderBase
{
    /// <summary>
    /// Searches the places.sqlite db and returns all bookmarks
    /// </summary>
    public override List<Bookmark> GetBookmarks()
    {
        return GetBookmarksFromPath(PlacesPath);
    }

    /// <summary>
    /// Path to places.sqlite
    /// </summary>
    private static string PlacesPath
    {
        get
        {
            var profileFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox");
            var profileIni = Path.Combine(profileFolderPath, @"profiles.ini");

            if (!File.Exists(profileIni))
                return string.Empty;

            // get firefox default profile directory from profiles.ini
            using var sReader = new StreamReader(profileIni);
            var ini = sReader.ReadToEnd();

            var lines = ini.Split("\r\n").ToList();

            var defaultProfileFolderNameRaw = lines.FirstOrDefault(x => x.Contains("Default=") && x != "Default=1") ?? string.Empty;

            if (string.IsNullOrEmpty(defaultProfileFolderNameRaw))
                return string.Empty;

            var defaultProfileFolderName = defaultProfileFolderNameRaw.Split('=').Last();

            var indexOfDefaultProfileAttributePath = lines.IndexOf("Path=" + defaultProfileFolderName);

            // Seen in the example above, the IsRelative attribute is always above the Path attribute
            var relativeAttribute = lines[indexOfDefaultProfileAttributePath - 1];

            return relativeAttribute == "0" // See above, the profile is located in a custom location, path is not relative, so IsRelative=0
                ? defaultProfileFolderName + @"\places.sqlite"
                : Path.Combine(profileFolderPath, defaultProfileFolderName) + @"\places.sqlite";
        }
    }
}

public static class Extensions
{
    public static IEnumerable<T> Select<T>(this SqliteDataReader reader, Func<SqliteDataReader, T> projection)
    {
        while (reader.Read())
        {
            yield return projection(reader);
        }
    }
}
