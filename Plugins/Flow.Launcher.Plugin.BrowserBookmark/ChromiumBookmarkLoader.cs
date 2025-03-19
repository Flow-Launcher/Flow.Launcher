using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Data.SQLite;
using SkiaSharp;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public abstract class ChromiumBookmarkLoader : IBookmarkLoader
{
    private readonly string _faviconCacheDir;

    protected ChromiumBookmarkLoader()
    {
        _faviconCacheDir = Path.Combine(
            Path.GetDirectoryName(typeof(ChromiumBookmarkLoader).Assembly.Location),
            "FaviconCache");
        Directory.CreateDirectory(_faviconCacheDir);
    }

    public abstract List<Bookmark> GetBookmarks();

    protected List<Bookmark> LoadBookmarks(string browserDataPath, string name)
    {
        var bookmarks = new List<Bookmark>();
        if (!Directory.Exists(browserDataPath)) return bookmarks;
        var paths = Directory.GetDirectories(browserDataPath);

        foreach (var profile in paths)
        {
            var bookmarkPath = Path.Combine(profile, "Bookmarks");
            if (!File.Exists(bookmarkPath))
                continue;

            // Register bookmark file monitoring (direct call to Main.RegisterBookmarkFile)
            try
            {
                if (File.Exists(bookmarkPath))
                {
                    //Main.RegisterBookmarkFile(bookmarkPath);
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to register bookmark file monitoring: {bookmarkPath}", ex);
            }

            var source = name + (Path.GetFileName(profile) == "Default" ? "" : $" ({Path.GetFileName(profile)})");
            var profileBookmarks = LoadBookmarksFromFile(bookmarkPath, source);

            // Load favicons after loading bookmarks
            var faviconDbPath = Path.Combine(profile, "Favicons");
            if (File.Exists(faviconDbPath))
            {
                LoadFaviconsFromDb(faviconDbPath, profileBookmarks);
            }

            bookmarks.AddRange(profileBookmarks);
        }

        return bookmarks;
    }

    protected List<Bookmark> LoadBookmarksFromFile(string path, string source)
    {
        var bookmarks = new List<Bookmark>();

        if (!File.Exists(path))
            return bookmarks;

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
        if (!jsonDocument.RootElement.TryGetProperty("roots", out var rootElement))
            return bookmarks;
        EnumerateRoot(rootElement, bookmarks, source);
        return bookmarks;
    }

    private void EnumerateRoot(JsonElement rootElement, ICollection<Bookmark> bookmarks, string source)
    {
        foreach (var folder in rootElement.EnumerateObject())
        {
            if (folder.Value.ValueKind != JsonValueKind.Object)
                continue;

            // Fix for Opera. It stores bookmarks slightly different than chrome.
            if (folder.Name == "custom_root")
                EnumerateRoot(folder.Value, bookmarks, source);
            else
                EnumerateFolderBookmark(folder.Value, bookmarks, source);
        }
    }

    private void EnumerateFolderBookmark(JsonElement folderElement, ICollection<Bookmark> bookmarks,
        string source)
    {
        if (!folderElement.TryGetProperty("children", out var childrenElement))
            return;
        foreach (var subElement in childrenElement.EnumerateArray())
        {
            if (subElement.TryGetProperty("type", out var type))
            {
                switch (type.GetString())
                {
                    case "folder":
                    case "workspace": // Edge Workspace
                        EnumerateFolderBookmark(subElement, bookmarks, source);
                        break;
                    default:
                        bookmarks.Add(new Bookmark(
                            subElement.GetProperty("name").GetString(),
                            subElement.GetProperty("url").GetString(),
                            source));
                        break;
                }
            }
            else
            {
                Log.Error(
                    $"ChromiumBookmarkLoader: EnumerateFolderBookmark: type property not found for {subElement.GetString()}");
            }
        }
    }

    private void LoadFaviconsFromDb(string dbPath, List<Bookmark> bookmarks)
    {
        try
        {
            // Use a copy to avoid lock issues with the original file
            var tempDbPath = Path.Combine(_faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.db");

            try
            {
                File.Copy(dbPath, tempDbPath, true);
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to copy favicon DB: {dbPath}", ex);
                return;
            }

            try
            {
                using var connection = new SQLiteConnection($"Data Source={tempDbPath};Version=3;Read Only=True;");
                connection.Open();

                foreach (var bookmark in bookmarks)
                {
                    try
                    {
                        var url = bookmark.Url;
                        if (string.IsNullOrEmpty(url)) continue;

                        // Extract domain from URL
                        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                            continue;

                        var domain = uri.Host;

                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
                            SELECT f.id, b.image_data
                            FROM favicons f
                            JOIN favicon_bitmaps b ON f.id = b.icon_id
                            JOIN icon_mapping m ON f.id = m.icon_id
                            WHERE m.page_url LIKE @url
                            ORDER BY b.width DESC
                            LIMIT 1";

                        cmd.Parameters.AddWithValue("@url", $"%{domain}%");

                        using var reader = cmd.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(1))
                        {
                            var iconId = reader.GetInt64(0).ToString();
                            var imageData = (byte[])reader["image_data"];

                            if (imageData != null && imageData.Length > 0)
                            {
                                var faviconPath = Path.Combine(_faviconCacheDir, $"{domain}_{iconId}.png");
                                if (!File.Exists(faviconPath))
                                {
                                    SaveBitmapData(imageData, faviconPath);
                                }
                                bookmark.FaviconPath = faviconPath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"Failed to extract bookmark favicon: {bookmark.Url}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to connect to SQLite: {tempDbPath}", ex);
            }

            // Delete temporary file
            try { File.Delete(tempDbPath); } catch { /* Ignore */ }
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to load favicon DB: {dbPath}", ex);
        }
    }

    private void SaveBitmapData(byte[] imageData, string outputPath)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            using var bitmap = SKBitmap.Decode(ms);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(outputPath);
                data.SaveTo(fs);
            }
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to save image: {outputPath}", ex);
        }
    }
}
