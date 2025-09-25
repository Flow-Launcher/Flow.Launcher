#nullable enable
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public class FirefoxBookmarkLoader : IBookmarkLoader
{
    private readonly string _browserName;
    private readonly string _placesPath;
    private readonly Action<string, string, Exception?> _logException;
    private readonly string _tempPath;

    public string Name => _browserName;

    private const string QueryAllBookmarks = """
        SELECT moz_places.url, moz_bookmarks.title
        FROM moz_places
            INNER JOIN moz_bookmarks ON (
                moz_bookmarks.fk NOT NULL AND moz_bookmarks.title NOT NULL AND moz_bookmarks.fk = moz_places.id
            )
        ORDER BY moz_places.visit_count DESC
        """;

    public FirefoxBookmarkLoader(string browserName, string placesPath, string tempPath, Action<string, string, Exception?> logException)
    {
        _browserName = browserName;
        _placesPath = placesPath;
        _logException = logException;
        _tempPath = tempPath;
    }

    public async IAsyncEnumerable<Bookmark> GetBookmarksAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_placesPath) || !File.Exists(_placesPath))
            yield break;

        var bookmarks = new List<Bookmark>();
        string? tempDbPath = null;

        try
        {
            // First, try to read directly from the source to avoid a slow file copy
            await ReadBookmarksFromDb(_placesPath, bookmarks, cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) // 5 is SQLITE_BUSY, 6 is SQLITE_LOCKED
        {
            // Fallback to copying the file if the database is locked (e.g., Firefox is open)
            try
            {
                tempDbPath = Path.Combine(_tempPath, $"ff_places_{Guid.NewGuid()}.sqlite");
                File.Copy(_placesPath, tempDbPath, true);
                await ReadBookmarksFromDb(tempDbPath, bookmarks, cancellationToken);
            }
            catch (Exception copyEx)
            {
                _logException(nameof(FirefoxBookmarkLoader), $"Failed to load {_browserName} bookmarks from fallback copy: {_placesPath}", copyEx);
            }
        }
        catch (Exception e)
        {
            _logException(nameof(FirefoxBookmarkLoader), $"Failed to load {_browserName} bookmarks: {_placesPath}", e);
        }
        finally
        {
            if (tempDbPath != null && File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } 
                catch(Exception e) { _logException(nameof(FirefoxBookmarkLoader), $"Failed to delete temp db file {tempDbPath}", e); }
            }
        }

        foreach (var bookmark in bookmarks)
        {
            yield return bookmark;
        }
    }

    private async Task ReadBookmarksFromDb(string dbPath, ICollection<Bookmark> bookmarks, CancellationToken cancellationToken)
    {
        var profilePath = Path.GetDirectoryName(dbPath) ?? string.Empty;
        var connectionString = $"Data Source={dbPath};Mode=ReadOnly;Pooling=false;";
        
        await using var dbConnection = new SqliteConnection(connectionString);
        await dbConnection.OpenAsync(cancellationToken);
        await using var command = new SqliteCommand(QueryAllBookmarks, dbConnection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var title = reader["title"]?.ToString() ?? string.Empty;
            var url = reader["url"]?.ToString();

            if (!string.IsNullOrEmpty(url))
            {
                bookmarks.Add(new Bookmark(title, url, _browserName, profilePath));
            }
        }
    }
}
