﻿using System;
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
        FaviconHelper.LoadFaviconsFromDb(_faviconCacheDir, dbPath, (tempDbPath) =>
        {
            // Since some bookmarks may have same favicon id, we need to record them to avoid duplicates
            var savedPaths = new ConcurrentDictionary<string, bool>();

            // Get favicons based on bookmarks concurrently
            Parallel.ForEach(bookmarks, bookmark =>
            {
                // Use read-only connection to avoid locking issues
                // Do not use pooling so that we do not need to clear pool: https://github.com/dotnet/efcore/issues/26580
                var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly;Pooling=false");
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
                    if (FaviconHelper.IsSvgData(imageData))
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
                        FaviconHelper.SaveBitmapData(imageData, faviconPath);
                    }

                    bookmark.FaviconPath = faviconPath;
                }
                catch (Exception ex)
                {
                    Main._context.API.LogException(ClassName, $"Failed to extract Firefox favicon: {bookmark.Url}", ex);
                }
                finally
                {
                    // Cache connection and clear pool after all operations to avoid issue:
                    // ObjectDisposedException: Safe handle has been closed.
                    connection.Close();
                    connection.Dispose();
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
        return GetBookmarksFromPath(PlacesPath);
    }

    /// <summary>
    /// Path to places.sqlite
    /// </summary>
    /// <remarks></remarks>
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

            /*
                Current profiles.ini structure example as of Firefox version 69.0.1

                [Install736426B0AF4A39CB]
                Default=Profiles/7789f565.default-release   <== this is the default profile this plugin will get the bookmarks from. When opened Firefox will load the default profile
                Locked=1

                [Profile2]
                Name=newblahprofile
                IsRelative=0
                Path=C:\t6h2yuq8.newblahprofile  <== Note this is a custom location path for the profile user can set, we need to cater for this in code.

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
            // Seen in the example above, the IsRelative attribute is always above the Path attribute

            var relativePath = Path.Combine(defaultProfileFolderName, "places.sqlite");
            var absoluePath = Path.Combine(profileFolderPath, relativePath);

            // If the index is out of range, it means that the default profile is in a custom location or the file is malformed
            // If the profile is in a custom location, we need to check 
            if (indexOfDefaultProfileAttributePath - 1 < 0 ||
                indexOfDefaultProfileAttributePath - 1 >= lines.Count)
            {
                return Directory.Exists(absoluePath) ? absoluePath : relativePath;
            }

            var relativeAttribute = lines[indexOfDefaultProfileAttributePath - 1];

            return relativeAttribute == "0" // See above, the profile is located in a custom location, path is not relative, so IsRelative=0
                ? relativePath : absoluePath;
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
