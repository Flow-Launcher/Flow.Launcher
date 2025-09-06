using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.BrowserBookmark.Helper;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Data.Sqlite;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public abstract class ChromiumBookmarkLoader : IBookmarkLoader
{
    private static readonly string ClassName = nameof(ChromiumBookmarkLoader);

    private readonly string _faviconCacheDir;

    protected ChromiumBookmarkLoader()
    {
        _faviconCacheDir = Main._faviconCacheDir;
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
                    Main.RegisterBookmarkFile(bookmarkPath);
                }
            }
            catch (Exception ex)
            {
                Main.Context.API.LogException(ClassName, $"Failed to register bookmark file monitoring: {bookmarkPath}", ex);
                continue;
            }

            var source = name + (Path.GetFileName(profile) == "Default" ? "" : $" ({Path.GetFileName(profile)})");
            var profileBookmarks = LoadBookmarksFromFile(bookmarkPath, source);

            // Load favicons after loading bookmarks
            if (Main._settings.EnableFavicons)
            {
                var faviconDbPath = Path.Combine(profile, "Favicons");
                if (File.Exists(faviconDbPath))
                {
                    Main.Context.API.StopwatchLogInfo(ClassName, $"Load {profileBookmarks.Count} favicons cost", () =>
                    {
                        LoadFaviconsFromDb(faviconDbPath, profileBookmarks);
                    });
                }
            }

            bookmarks.AddRange(profileBookmarks);
        }

        return bookmarks;
    }

    protected static List<Bookmark> LoadBookmarksFromFile(string path, string source)
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

    private static void EnumerateRoot(JsonElement rootElement, ICollection<Bookmark> bookmarks, string source)
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

    private static void EnumerateFolderBookmark(JsonElement folderElement, ICollection<Bookmark> bookmarks,
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
                Main.Context.API.LogError(ClassName, $"type property not found for {subElement.GetString()}");
            }
        }
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
                    var url = bookmark.Url;
                    if (string.IsNullOrEmpty(url)) return;

                    // Extract domain from URL
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                        return;

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
                    if (!reader.Read() || reader.IsDBNull(1))
                        return;

                    var iconId = reader.GetInt64(0).ToString();
                    var imageData = (byte[])reader["image_data"];

                    if (imageData is not { Length: > 0 })
                        return;

                    var faviconPath = Path.Combine(_faviconCacheDir, $"chromium_{domain}_{iconId}.png");

                    // Filter out duplicate favicons
                    if (savedPaths.TryAdd(faviconPath, true))
                    {
                        FaviconHelper.SaveBitmapData(imageData, faviconPath);
                    }

                    bookmark.FaviconPath = faviconPath;
                }
                catch (Exception ex)
                {
                    Main.Context.API.LogException(ClassName, $"Failed to extract bookmark favicon: {bookmark.Url}", ex);
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
