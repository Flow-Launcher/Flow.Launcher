#nullable enable
using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public class LocalFaviconExtractor
{
    private readonly PluginInitContext _context;
    private readonly string _tempPath;

    public LocalFaviconExtractor(PluginInitContext context, string tempPath)
    {
        _context = context;
        _tempPath = tempPath;
    }

    public async Task<byte[]?> GetFaviconDataAsync(Bookmark bookmark, CancellationToken token)
    {
        return bookmark.Source switch
        {
            var s when s.Contains("Firefox") => await GetFirefoxFaviconAsync(bookmark, token),
            _ => await GetChromiumFaviconAsync(bookmark, token) // Default to Chromium
        };
    }

    private async Task<byte[]?> GetChromiumFaviconAsync(Bookmark bookmark, CancellationToken token)
    {
        var dbPath = Path.Combine(bookmark.ProfilePath, "Favicons");
        if (!File.Exists(dbPath)) return null;

        var tempDbPath = Path.Combine(_tempPath, $"chromium_favicons_{Guid.NewGuid()}.db");
        try
        {
            File.Copy(dbPath, tempDbPath, true);

            var query = @"
                SELECT b.image_data FROM favicon_bitmaps b
                JOIN icon_mapping m ON b.icon_id = m.icon_id
                WHERE m.page_url = @url
                ORDER BY b.width DESC LIMIT 1";

            var connectionString = $"Data Source={tempDbPath};Mode=ReadOnly;Pooling=false;";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(token);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@url", bookmark.Url);

            var result = await cmd.ExecuteScalarAsync(token);
            if (result is byte[] data && data.Length > 0)
            {
                _context.API.LogDebug(nameof(LocalFaviconExtractor), $"Extracted {data.Length} bytes for {bookmark.Url} from Chromium DB.");
                return data;
            }
            return null;
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(LocalFaviconExtractor), $"Failed to extract Chromium favicon for {bookmark.Url} from {bookmark.Source}", ex);
            return null;
        }
        finally
        {
            CleanupTempFiles(tempDbPath);
        }
    }
    
    private async Task<byte[]?> GetFirefoxFaviconAsync(Bookmark bookmark, CancellationToken token)
    {
        var dbPath = Path.Combine(bookmark.ProfilePath, "favicons.sqlite");
        if (!File.Exists(dbPath)) return null;

        var tempDbPath = Path.Combine(_tempPath, $"firefox_favicons_{Guid.NewGuid()}.sqlite");
        try
        {
            File.Copy(dbPath, tempDbPath, true);
            
            var query = @"
                SELECT i.data FROM moz_icons i
                JOIN moz_icons_to_pages ip ON i.id = ip.icon_id
                JOIN moz_pages_w_icons p ON ip.page_id = p.id
                WHERE p.page_url = @url
                ORDER BY i.width DESC LIMIT 1";

            var connectionString = $"Data Source={tempDbPath};Mode=ReadOnly;Pooling=false;";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(token);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@url", bookmark.Url);

            var result = await cmd.ExecuteScalarAsync(token);
            if (result is not byte[] imageData || imageData.Length == 0)
                return null;

            _context.API.LogDebug(nameof(LocalFaviconExtractor), $"Extracted {imageData.Length} bytes for {bookmark.Url} from Firefox DB.");
            
            // Handle old GZipped favicons
            if (imageData.Length > 2 && imageData[0] == 0x1f && imageData[1] == 0x8b)
            {
                await using var inputStream = new MemoryStream(imageData);
                await using var gZipStream = new GZipStream(inputStream, CompressionMode.Decompress);
                await using var outputStream = new MemoryStream();
                await gZipStream.CopyToAsync(outputStream, token);
                return outputStream.ToArray();
            }

            return imageData;
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(LocalFaviconExtractor), $"Failed to extract Firefox favicon for {bookmark.Url} from {bookmark.Source}", ex);
            return null;
        }
        finally
        {
            CleanupTempFiles(tempDbPath);
        }
    }

    private void CleanupTempFiles(string mainTempDbPath)
    {
        // This method ensures that the main temp file and any of its associated files
        // (e.g., -wal, -shm) are deleted.
        try
        {
            var directory = Path.GetDirectoryName(mainTempDbPath);
            var baseName = Path.GetFileName(mainTempDbPath);
            if (directory == null || !Directory.Exists(directory)) return;

            foreach (var file in Directory.GetFiles(directory, baseName + "*"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    // Log failure to delete a specific chunk, but don't stop the process
                    _context.API.LogException(nameof(LocalFaviconExtractor), $"Failed to delete temporary file chunk: {file}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(LocalFaviconExtractor), $"Failed to clean up temporary files for base: {mainTempDbPath}", ex);
        }
    }
}
