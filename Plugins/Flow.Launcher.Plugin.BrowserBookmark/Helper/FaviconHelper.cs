using System;
using System.Collections.Generic;
using System.IO;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Data.Sqlite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Flow.Launcher.Plugin.BrowserBookmark.Helper;

public static class FaviconHelper
{
    private static readonly string ClassName = nameof(FaviconHelper);

    private static void ExecuteWithTempDb(string faviconCacheDir, string dbPath, Action<string> action)
    {
        var tempDbPath = Path.Combine(faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.db");
        try
        {
            File.Copy(dbPath, tempDbPath, true);
            using (var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadWrite"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE INDEX IF NOT EXISTS idx_moz_pages_w_icons_page_url ON moz_pages_w_icons(page_url);";
                try { command.ExecuteNonQuery(); } catch (SqliteException) { /* ignore */ }
                command.CommandText = "CREATE INDEX IF NOT EXISTS idx_icon_mapping_page_url ON icon_mapping(page_url);";
                try { command.ExecuteNonQuery(); } catch (SqliteException) { /* ignore */ }
            }
            action(tempDbPath);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to process or index SQLite DB: {dbPath}", ex);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch (Exception ex) { Main._context.API.LogException(ClassName, $"Failed to delete temp favicon DB: {tempDbPath}", ex); }
            }
        }
    }

    public static void ProcessFavicons(
        string dbPath,
        string faviconCacheDir,
        List<Bookmark> bookmarks,
        string sqlQuery,
        string patternPrefix,
        Func<SqliteDataReader, (string Id, byte[] Data)> imageDataExtractor,
        Func<Uri, string, byte[], string> pathBuilder)
    {
        if (!File.Exists(dbPath)) return;

        ExecuteWithTempDb(faviconCacheDir, dbPath, tempDbPath =>
        {
            var savedPaths = new Dictionary<string, bool>();
            using var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly;Pooling=false");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sqlQuery;
            var patternParam = cmd.CreateParameter();
            patternParam.ParameterName = "@pattern";
            cmd.Parameters.Add(patternParam);

            foreach (var bookmark in bookmarks)
            {
                try
                {
                    if (string.IsNullOrEmpty(bookmark.Url) || !Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var uri))
                        continue;

                    patternParam.Value = $"{patternPrefix}{uri.Host}/*";

                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        continue;

                    var (id, imageData) = imageDataExtractor(reader);
                    if (imageData is not { Length: > 0 })
                        continue;

                    var faviconPath = pathBuilder(uri, id, imageData);
                    if (savedPaths.TryAdd(faviconPath, true))
                    {
                        SaveBitmapData(imageData, faviconPath);
                    }
                    bookmark.FaviconPath = faviconPath;
                }
                catch (Exception ex)
                {
                    Main._context.API.LogException(ClassName, $"Failed to extract favicon for: {bookmark.Url}", ex);
                }
            }
        });
    }

    public static void SaveBitmapData(byte[] imageData, string outputPath)
    {
        try
        {
            // Attempt to load the image data. This will handle all formats ImageSharp
            // supports, including SVG (if the necessary decoders are present) and common
            // raster formats. It will throw an exception for malformed images.
            using var image = Image.Load(imageData);

            // Resize the image to a maximum of 64x64.
            var options = new ResizeOptions
            {
                Size = new Size(64, 64),
                Mode = ResizeMode.Max
            };
            image.Mutate(x => x.Resize(options));

            // Always save as PNG for maximum compatibility with the UI renderer.
            image.SaveAsPng(outputPath, new PngEncoder { CompressionLevel = PngCompressionLevel.DefaultCompression });
        }
        catch (Exception ex)
        {
            // This will now catch errors from loading malformed SVGs or other image types,
            // preventing them from being saved and crashing the UI.
            Main._context.API.LogException(ClassName, $"Failed to load/resize/save image to {outputPath}. It may be a malformed image.", ex);
        }
    }
}
