#nullable enable
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

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
        var profilePath = bookmark.ProfilePath;
        if (!string.IsNullOrEmpty(profilePath))
        {
            if (File.Exists(Path.Combine(profilePath, "favicons.sqlite")))
                return await GetFirefoxFaviconAsync(bookmark, token);

            if (File.Exists(Path.Combine(profilePath, "Favicons")))
                return await GetChromiumFaviconAsync(bookmark, token);
        }

        return bookmark.Source?.IndexOf("Firefox", StringComparison.OrdinalIgnoreCase) >= 0
            ? await GetFirefoxFaviconAsync(bookmark, token)
            : await GetChromiumFaviconAsync(bookmark, token);
    }

    private Task<byte[]?> GetChromiumFaviconAsync(Bookmark bookmark, CancellationToken token)
    {
        const string query = @"
            SELECT b.image_data FROM favicon_bitmaps b
            JOIN icon_mapping m ON b.icon_id = m.icon_id
            WHERE m.page_url = @url
            ORDER BY b.width DESC LIMIT 1";

        return GetFaviconFromDbAsync(bookmark, "Favicons", query, null, token);
    }

    private Task<byte[]?> GetFirefoxFaviconAsync(Bookmark bookmark, CancellationToken token)
    {
        const string query = @"
            SELECT i.data FROM moz_icons i
            JOIN moz_icons_to_pages ip ON i.id = ip.icon_id
            JOIN moz_pages_w_icons p ON ip.page_id = p.id
            WHERE p.page_url = @url
            ORDER BY i.width DESC LIMIT 1";

        return GetFaviconFromDbAsync(bookmark, "favicons.sqlite", query, PostProcessFirefoxFavicon, token);
    }

    private async Task<byte[]?> GetFaviconFromDbAsync(Bookmark bookmark, string dbFileName, string query,
        Func<byte[], CancellationToken, Task<byte[]>>? postProcessor, CancellationToken token)
    {
        var dbPath = Path.Combine(bookmark.ProfilePath, dbFileName);
        if (!File.Exists(dbPath))
            return null;

        var tempDbPath = Path.Combine(_tempPath, $"{Path.GetFileNameWithoutExtension(dbFileName)}_{Guid.NewGuid()}{Path.GetExtension(dbFileName)}");

        try
        {
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";

            File.Copy(dbPath, tempDbPath, true);
            if (File.Exists(walPath))
                File.Copy(walPath, tempDbPath + "-wal", true);
            if (File.Exists(shmPath))
                File.Copy(shmPath, tempDbPath + "-shm", true);
            var connectionString = $"Data Source={tempDbPath};Mode=ReadOnly;Pooling=false;";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(token);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@url", bookmark.Url);

            if (await cmd.ExecuteScalarAsync(token) is not byte[] data || data.Length == 0)
                return null;

            _context.API.LogDebug(nameof(LocalFaviconExtractor), $"Extracted {data.Length} bytes for {bookmark.Url} from {dbFileName}.");

            return postProcessor != null ? await postProcessor(data, token) : data;
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(LocalFaviconExtractor), $"Failed to extract favicon for {bookmark.Url} from {bookmark.Source}'s {dbFileName}", ex);
            return null;
        }
        finally
        {
            CleanupTempFiles(tempDbPath);
        }
    }

    private async Task<byte[]> PostProcessFirefoxFavicon(byte[] imageData, CancellationToken token)
    {
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
