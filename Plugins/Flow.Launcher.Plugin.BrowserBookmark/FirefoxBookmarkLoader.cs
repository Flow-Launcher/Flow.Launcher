using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public abstract class FirefoxBookmarkLoaderBase : IBookmarkLoader
{
    private readonly string _faviconCacheDir;

    protected FirefoxBookmarkLoaderBase()
    {
        _faviconCacheDir = Path.Combine(
            Path.GetDirectoryName(typeof(FirefoxBookmarkLoaderBase).Assembly.Location),
            "FaviconCache");
        Directory.CreateDirectory(_faviconCacheDir);
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

    private const string DbPathFormat = "Data Source={0}";

    protected List<Bookmark> GetBookmarksFromPath(string placesPath)
    {
        // Variable to store bookmark list
        var bookmarks = new List<Bookmark>();

        // Return empty list if places.sqlite file doesn't exist
        if (string.IsNullOrEmpty(placesPath) || !File.Exists(placesPath))
            return bookmarks;

        try
        {
            // Try to register file monitoring
            try
            {
                Main.RegisterBookmarkFile(placesPath);
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to register Firefox bookmark file monitoring: {placesPath}", ex);
            }

            // Use a copy to avoid lock issues with the original file
            var tempDbPath = Path.Combine(_faviconCacheDir, $"tempplaces_{Guid.NewGuid()}.sqlite");
            File.Copy(placesPath, tempDbPath, true);

            // Connect to database and execute query
            string dbPath = string.Format(DbPathFormat, tempDbPath);
            using var dbConnection = new SqliteConnection(dbPath);
            dbConnection.Open();
            var reader = new SqliteCommand(QueryAllBookmarks, dbConnection).ExecuteReader();

            // Create bookmark list
            bookmarks = reader
                .Select(
                    x => new Bookmark(
                        x["title"] is DBNull ? string.Empty : x["title"].ToString(),
                        x["url"].ToString(),
                        "Firefox"
                    )
                )
                .ToList();

            // Path to favicon database
            var faviconDbPath = Path.Combine(Path.GetDirectoryName(placesPath), "favicons.sqlite");
            if (File.Exists(faviconDbPath))
            {
                LoadFaviconsFromDb(faviconDbPath, bookmarks);
            }
            // https://github.com/dotnet/efcore/issues/26580
            SqliteConnection.ClearPool(dbConnection);
            dbConnection.Close();

            // Delete temporary file
            try
            {
                File.Delete(tempDbPath);
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to delete temporary favicon DB: {tempDbPath}", ex);
            }
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to load Firefox bookmarks: {placesPath}", ex);
        }

        return bookmarks;
    }

    private void LoadFaviconsFromDb(string faviconDbPath, List<Bookmark> bookmarks)
{
    try
    {
        // Use a copy to avoid lock issues with the original file
        var tempDbPath = Path.Combine(_faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.sqlite");
        File.Copy(faviconDbPath, tempDbPath, true);

        string dbPath = string.Format(DbPathFormat, tempDbPath);
        using var connection = new SqliteConnection(dbPath);
        connection.Open();

        // Get favicons based on bookmark URLs
        foreach (var bookmark in bookmarks)
        {
            try
            {
                if (string.IsNullOrEmpty(bookmark.Url))
                    continue;

                // Extract domain from URL
                if (!Uri.TryCreate(bookmark.Url, UriKind.Absolute, out Uri uri))
                    continue;

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
                    continue;

                var imageData = (byte[])reader["data"];

                if (imageData is not { Length: > 0 })
                    continue;

                var faviconPath = Path.Combine(_faviconCacheDir, $"firefox_{domain}.png");

                if (!File.Exists(faviconPath))
                {
                    if (IsSvgData(imageData))
                    {
                        // SVG를 PNG로 변환
                        var pngData = ConvertSvgToPng(imageData);
                        if (pngData != null)
                        {
                            SaveBitmapData(pngData, faviconPath);
                        }
                        else
                        {
                            // Set empty string on conversion failure (will use default icon)
                            bookmark.FaviconPath = string.Empty;
                            continue;
                        }
                    }
                    else
                    {
                        // Save PNG directly
                        SaveBitmapData(imageData, faviconPath);
                    }
                }

                bookmark.FaviconPath = faviconPath;
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to extract Firefox favicon: {bookmark.Url}", ex);
            }
        }

        // https://github.com/dotnet/efcore/issues/26580
        SqliteConnection.ClearPool(connection);
        connection.Close();

        // Delete temporary file
        try
        {
            File.Delete(tempDbPath);
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to delete temporary favicon DB: {tempDbPath}", ex);
        }
    }
    catch (Exception ex)
    {
        Log.Exception($"Failed to load Firefox favicon DB: {faviconDbPath}", ex);
    }
}

    private byte[] ConvertSvgToPng(byte[] svgData)
    {
        try
        {
            // Create SKSvg object from SVG data
            using var stream = new MemoryStream(svgData);
            var svg = new SkiaSharp.Extended.Svg.SKSvg();
            svg.Load(stream);

            // Set default values if SVG size is invalid or missing
            float width = svg.Picture.CullRect.Width > 0 ? svg.Picture.CullRect.Width : 32;
            float height = svg.Picture.CullRect.Height > 0 ? svg.Picture.CullRect.Height : 32;
        
            // Calculate scale for 32x32 favicon size
            float scaleX = 32 / width;
            float scaleY = 32 / height;
            float scale = Math.Min(scaleX, scaleY);
        
            // Calculate final image dimensions (maintaining aspect ratio)
            int finalWidth = (int)(width * scale);
            int finalHeight = (int)(height * scale);
        
            // Render to PNG
            using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(finalWidth, finalHeight));
            var canvas = surface.Canvas;
        
            // Set transparent background
            canvas.Clear(SkiaSharp.SKColors.Transparent);
        
            // Draw SVG (scaled to fit)
            canvas.Scale(scale);
            canvas.DrawPicture(svg.Picture);
        
            // Extract image data
            using var image = surface.Snapshot();
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            Log.Exception("SVG to PNG conversion failed", ex);
            return null;
        }
    }

    private static bool IsSvgData(byte[] data)
    {
        if (data == null || data.Length < 5)
            return false;

        // Check SVG file signature
        // Verify SVG XML header starting with ASCII
        string header = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 200)).ToLower();

        return header.Contains("<svg") ||
               header.StartsWith("<?xml") && header.Contains("<svg") ||
               header.Contains("image/svg+xml");
    }

    private static void SaveBitmapData(byte[] imageData, string outputPath)
    {
        try
        {
            File.WriteAllBytes(outputPath, imageData);
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to save image: {outputPath}", ex);
        }
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
