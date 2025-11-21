#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public class ChromiumBookmarkLoader : IBookmarkLoader
{
    private readonly string _browserName;
    private readonly string _browserDataPath;
    private readonly ConcurrentBag<string> _discoveredFiles;

    public string Name => _browserName;

    public ChromiumBookmarkLoader(string browserName, string browserDataPath, ConcurrentBag<string> discoveredFiles)
    {
        _browserName = browserName;
        _browserDataPath = browserDataPath;
        _discoveredFiles = discoveredFiles;
    }

    public async IAsyncEnumerable<Bookmark> GetBookmarksAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_browserDataPath))
            yield break;

        var profileDirectories = BrowserDetector.GetChromiumProfileDirectories(_browserDataPath);

        foreach (var profilePath in profileDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bookmarkPath = Path.Combine(profilePath, "Bookmarks");
            if (!File.Exists(bookmarkPath))
                continue;

            _discoveredFiles.Add(bookmarkPath);
            var source = _browserName + (Path.GetFileName(profilePath) == "Default" ? "" : $" ({Path.GetFileName(profilePath)})");

            var bookmarks = new List<Bookmark>();
            try
            {
                await using var stream = File.OpenRead(bookmarkPath);
                using var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (jsonDocument.RootElement.TryGetProperty("roots", out var rootElement))
                {
                    bookmarks.AddRange(EnumerateBookmarks(rootElement, source, profilePath));
                }
            }
            catch (IOException ex)
            {
                Main.Context.API.LogException(nameof(ChromiumBookmarkLoader), $"IO error reading {_browserName} bookmarks: {bookmarkPath}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Main.Context.API.LogException(nameof(ChromiumBookmarkLoader), $"Unauthorized to read {_browserName} bookmarks: {bookmarkPath}", ex);
            }
            catch (JsonException ex)
            {
                Main.Context.API.LogException(nameof(ChromiumBookmarkLoader), $"Failed to parse bookmarks file for {_browserName}: {bookmarkPath}", ex);
            }
            catch (Exception ex)
            {
                Main.Context.API.LogException(nameof(ChromiumBookmarkLoader), $"Unexpected error loading {_browserName} bookmarks: {bookmarkPath}", ex);
            }

            foreach (var bookmark in bookmarks)
            {
                yield return bookmark;
            }
        }
    }

    private IEnumerable<Bookmark> EnumerateBookmarks(JsonElement rootElement, string source, string profilePath)
    {
        var bookmarks = new List<Bookmark>();
        foreach (var folder in rootElement.EnumerateObject())
        {
            if (folder.Value.ValueKind != JsonValueKind.Object)
                continue;

            // Fix for Opera. It stores bookmarks slightly different than chrome.
            if (folder.Name == "custom_root")
                bookmarks.AddRange(EnumerateBookmarks(folder.Value, source, profilePath));
            else
                EnumerateFolderBookmark(folder.Value, bookmarks, source, profilePath);
        }
        return bookmarks;
    }

    private void EnumerateFolderBookmark(JsonElement folderElement, ICollection<Bookmark> bookmarks, string source, string profilePath)
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
                        EnumerateFolderBookmark(subElement, bookmarks, source, profilePath);
                        break;
                    case "url":
                        if (subElement.TryGetProperty("name", out var name) &&
                            subElement.TryGetProperty("url", out var url) &&
                            !string.IsNullOrEmpty(name.GetString()) &&
                            !string.IsNullOrEmpty(url.GetString()))
                        {
                            bookmarks.Add(new Bookmark(name.GetString()!, url.GetString()!, source, profilePath));
                        }
                        break;
                }
            }
            else
            {
                Main.Context.API.LogException(nameof(ChromiumBookmarkLoader), "type property not found in bookmark node.", null);
            }
        }
    }
}
