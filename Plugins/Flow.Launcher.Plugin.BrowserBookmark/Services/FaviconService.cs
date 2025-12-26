#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public readonly record struct FaviconCandidate(string Url, int Score);

public partial class FaviconService : IDisposable
{
    private readonly Settings _settings;
    private readonly string _faviconCacheDir;
    private readonly LocalFaviconExtractor _localExtractor;
    private readonly FaviconWebClient _webClient;
    private readonly HtmlFaviconParser _htmlParser;
    private readonly ImageConverter _imageConverter;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, Task<string?>> _ongoingFetches = new(StringComparer.OrdinalIgnoreCase);
    private ConcurrentDictionary<string, DateTime> _failedFetches = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _failsFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private record struct FetchResult(byte[]? PngData, int Size);
    private static readonly TimeSpan FailedFaviconCooldown = TimeSpan.FromHours(24);

    public FaviconService(Settings settings, string tempPath)
    {
        _settings = settings;

        _faviconCacheDir = Path.Combine(Main.Context.CurrentPluginMetadata.PluginCacheDirectoryPath, "FaviconCache");
        Directory.CreateDirectory(_faviconCacheDir);

        var failsDir = Path.Combine(Main.Context.CurrentPluginMetadata.PluginCacheDirectoryPath, "FaviconFails");
        Directory.CreateDirectory(failsDir);
        _failsFilePath = Path.Combine(failsDir, "FaviconFails.json");

        LoadFailedFetches();

        _localExtractor = new LocalFaviconExtractor(tempPath);
        _webClient = new FaviconWebClient();
        _htmlParser = new HtmlFaviconParser();
        _imageConverter = new ImageConverter();
    }

    private void LoadFailedFetches()
    {
        if (!File.Exists(_failsFilePath)) return;

        try
        {
            var json = File.ReadAllText(_failsFilePath);
            var fails = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
            if (fails != null)
            {
                _failedFetches = new ConcurrentDictionary<string, DateTime>(fails, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Main.Context.API.LogException(nameof(FaviconService), $"Failed to load failed favicons file from {_failsFilePath}", ex);
        }
    }

    private async Task SaveFailedFetchesAsync()
    {
        var acquired = false;
        try
        {
            await _fileLock.WaitAsync(_cts.Token);
            acquired = true;
            var json = JsonSerializer.Serialize(_failedFetches);
            await File.WriteAllTextAsync(_failsFilePath, json, _cts.Token);
        }
        catch (OperationCanceledException) { /* Swallow if app is closing */ }
        catch (ObjectDisposedException) { /* Swallow if disposing */ }
        catch (Exception ex)
        {
            Main.Context.API.LogException(nameof(FaviconService), $"Failed to save failed favicons file to {_failsFilePath}", ex);
        }
        finally
        {
            if (acquired)
                _fileLock.Release();
        }
    }

    public async Task ProcessBookmarkFavicons(IReadOnlyList<Bookmark> bookmarks, CancellationToken cancellationToken)
    {
        if (!_settings.EnableFavicons) return;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(bookmarks, options, async (bookmark, token) =>
        {
            var pageCachePath = GetCachePath(bookmark.Url, _faviconCacheDir);
            var hostCachePath = Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var pageUri)
                ? GetCachePath(pageUri.GetLeftPart(UriPartial.Authority), _faviconCacheDir)
                : pageCachePath;
            if (File.Exists(hostCachePath))
            {
                bookmark.FaviconPath = hostCachePath;
                return;
            }
            if (File.Exists(pageCachePath))
            {
                bookmark.FaviconPath = pageCachePath;
                return;
            }
            // 1. Try local browser database
            var localData = await _localExtractor.GetFaviconDataAsync(bookmark, token);
            if (localData != null)
            {
                using var ms = new MemoryStream(localData, writable: false);
                var (pngData, _) = await _imageConverter.ToPngAsync(ms, token);
                if (pngData != null)
                {
                    var path = hostCachePath;
                    var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await File.WriteAllBytesAsync(tmp, pngData, token);
                        try { File.Move(tmp, path, overwrite: false); }
                        catch (IOException)
                        {
                            // Another thread may have created it concurrently.
                        }
                    }
                    finally
                    {
                        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
                    }
                    if (File.Exists(path))
                    {
                        bookmark.FaviconPath = path;
                        return;
                    }
                    // If write failed and file still doesn't exist, fall through to web fallback.
                }
            }

            // 2. Fallback to web if enabled
            if (_settings.FetchMissingFavicons && Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var uri))
            {
                var webFaviconPath = await GetFaviconFromWebAsync(uri, token);
                if (!string.IsNullOrEmpty(webFaviconPath))
                {
                    bookmark.FaviconPath = webFaviconPath;
                }
            }
        });

    }

    private async Task<string?> GetFaviconFromWebAsync(Uri url, CancellationToken token)
    {
        if (url is null || (url.Scheme != "http" && url.Scheme != "https"))
            return null;

        var authority = url.GetLeftPart(UriPartial.Authority);

        if (_failedFetches.TryGetValue(authority, out var lastAttemptTime) &&
            (DateTime.UtcNow - lastAttemptTime < FailedFaviconCooldown))
        {
            Main.Context.API.LogDebug(nameof(FaviconService),
                $"Skipping favicon fetch for {authority} due to recent failure (cooldown active).");
            return null;
        }

        var fetchTask = _ongoingFetches.GetOrAdd(authority, key => FetchAndCacheFaviconAsync(new Uri(key)));
        try
        {
            return await fetchTask.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Do not cancel the shared fetch; just stop waiting for this caller.
            return null;
        }
    }

    private static string GetCachePath(string url, string cacheDir)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return Path.Combine(cacheDir, sb.ToString() + ".png");
    }

    private async Task<string?> FetchAndCacheFaviconAsync(Uri url)
    {
        var urlString = url.GetLeftPart(UriPartial.Authority);
        var cachePath = GetCachePath(urlString, _faviconCacheDir);
        if (File.Exists(cachePath)) return cachePath;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        FetchResult icoResult = default;
        FetchResult htmlResult = default;
        bool fetchAttempted = false;

        try
        {
            var icoTask = FetchFromUrlAsync(new Uri(url, "/favicon.ico"), linkedCts.Token);
            var htmlTask = FetchFromHtmlAsync(url, linkedCts.Token);

            var tasks = new List<Task<FetchResult>> { icoTask, htmlTask };

            while (tasks.Any())
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                fetchAttempted = true;

                if (completedTask.IsCompletedSuccessfully && completedTask.Result.Size >= ImageConverter.TargetIconSize)
                {
                    linkedCts.Cancel();
                    break;
                }
            }

            try { icoResult = await icoTask; } catch (OperationCanceledException) { /* Expected */ }
            try { htmlResult = await htmlTask; } catch (OperationCanceledException) { /* Expected */ }

            var bestResult = SelectBestFavicon(icoResult, htmlResult);

            if (bestResult.PngData != null)
            {
                try
                {
                    await File.WriteAllBytesAsync(cachePath, bestResult.PngData, _cts.Token);
                }
                catch (IOException)
                {
                    // Another thread may have created it concurrently.
                }
                if (File.Exists(cachePath))
                {
                    Main.Context.API.LogDebug(nameof(FaviconService), $"Favicon for {urlString} cached successfully.");
                    if (_failedFetches.TryRemove(urlString, out _))
                    {
                        _ = SaveFailedFetchesAsync();
                    }
                    return cachePath;
                }
                // Do not treat as success; let finally record failure if needed.
            }

            Main.Context.API.LogDebug(nameof(FaviconService), $"No suitable favicon found for {urlString} after all tasks.");
        }
        catch (Exception ex)
        {
            Main.Context.API.LogException(nameof(FaviconService), $"Error in favicon fetch for {urlString}", ex);
            fetchAttempted = true;
        }
        finally
        {
            _ongoingFetches.TryRemove(urlString, out _);

            if (fetchAttempted && !File.Exists(cachePath))
            {
                _failedFetches[urlString] = DateTime.UtcNow;
                _ = SaveFailedFetchesAsync();
            }
        }

        return null;
    }

    private static FetchResult SelectBestFavicon(FetchResult icoResult, FetchResult htmlResult)
    {
        var htmlValid = htmlResult.PngData != null;
        var icoValid = icoResult.PngData != null;

        if (htmlValid && htmlResult.Size >= ImageConverter.TargetIconSize) return htmlResult;
        if (icoValid && icoResult.Size >= ImageConverter.TargetIconSize) return icoResult;

        if (htmlValid && icoValid) return htmlResult.Size >= icoResult.Size ? htmlResult : icoResult;
        if (htmlValid) return htmlResult;
        if (icoValid) return icoResult;
        return default;
    }

    private async Task<FetchResult> FetchFromHtmlAsync(Uri pageUri, CancellationToken token)
    {
        var htmlResult = await _webClient.GetHtmlHeadAsync(pageUri, token);
        if (htmlResult is not { Html: not null, BaseUri: not null })
            return default;

        var candidates = _htmlParser.Parse(htmlResult.Value.Html, htmlResult.Value.BaseUri);

        foreach (var candidate in candidates.OrderByDescending(c => c.Score))
        {
            if (Uri.TryCreate(candidate.Url, UriKind.Absolute, out var candidateUri))
            {
                var result = await FetchFromUrlAsync(candidateUri, token);
                if (result.PngData != null)
                {
                    return result;
                }
            }
        }

        return default;
    }

    private async Task<FetchResult> FetchFromUrlAsync(Uri faviconUri, CancellationToken token)
    {
        await using var stream = await _webClient.DownloadFaviconAsync(faviconUri, token);
        if (stream == null)
            return default;

        var (pngData, size) = await _imageConverter.ToPngAsync(stream, token);
        if (pngData is { Length: > 0 })
        {
            return new FetchResult(pngData, size);
        }

        return default;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _ongoingFetches.Clear();
        _webClient.Dispose();
        _fileLock.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
