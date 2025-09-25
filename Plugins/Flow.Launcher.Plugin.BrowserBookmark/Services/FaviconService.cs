#nullable enable

using Flow.Launcher.Plugin.BrowserBookmark.Models;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public partial class FaviconService : IDisposable
{
    private readonly PluginInitContext _context;
    private readonly Settings _settings;
    private readonly string _faviconCacheDir;
    private readonly HttpClient _httpClient;
    private readonly LocalFaviconExtractor _localExtractor;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, Task<string?>> _ongoingFetches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _failedFetches = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("<link[^>]+?>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex LinkTagRegex();
    [GeneratedRegex("rel\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RelAttributeRegex();
    [GeneratedRegex("href\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex HrefAttributeRegex();
    [GeneratedRegex("sizes\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex SizesAttributeRegex();
    [GeneratedRegex("<base[^>]+href\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex BaseHrefRegex();

    private record struct FaviconCandidate(string Url, int Score);
    private record struct FetchResult(string? TempPath, int Size);
    private const int MaxFaviconBytes = 250 * 1024;
    private const int TargetIconSize = 48;
    private static readonly TimeSpan FailedFaviconCooldown = TimeSpan.FromHours(12); // How long to wait before retrying a failed favicon

    public FaviconService(PluginInitContext context, Settings settings, string tempPath)
    {
        _context = context;
        _settings = settings;

        _faviconCacheDir = Path.Combine(context.CurrentPluginMetadata.PluginCacheDirectoryPath, "FaviconCache");
        Directory.CreateDirectory(_faviconCacheDir);

        _localExtractor = new LocalFaviconExtractor(context, tempPath);

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.6409.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task ProcessBookmarkFavicons(IReadOnlyList<Bookmark> bookmarks, CancellationToken cancellationToken)
    {
        if (!_settings.EnableFavicons) return;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(bookmarks, options, async (bookmark, token) =>
        {
            var cachePath = GetCachePath(bookmark.Url, _faviconCacheDir);
            if (File.Exists(cachePath))
            {
                bookmark.FaviconPath = cachePath;
                return;
            }

            // 1. Try local browser database
            var localData = await _localExtractor.GetFaviconDataAsync(bookmark, token);
            if (localData != null)
            {
                var (pngData, _) = await ToPng(new MemoryStream(localData), token);
                if (pngData != null)
                {
                    await File.WriteAllBytesAsync(cachePath, pngData, token);
                    bookmark.FaviconPath = cachePath;
                    return;
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

    private Task<string?> GetFaviconFromWebAsync(Uri url, CancellationToken token)
    {
        if (url is null || (url.Scheme != "http" && url.Scheme != "https"))
        {
            return Task.FromResult<string?>(null);
        }
        var authority = url.GetLeftPart(UriPartial.Authority);

        // Check if this domain recently failed to provide a favicon
        if (_failedFetches.TryGetValue(authority, out var lastAttemptTime) &&
            (DateTime.UtcNow - lastAttemptTime < FailedFaviconCooldown))
        {
            _context.API.LogDebug(nameof(FaviconService), $"Skipping favicon fetch for {authority} due to recent failure (cooldown active).");
            return Task.FromResult<string?>(null);
        }

        return _ongoingFetches.GetOrAdd(authority, key => FetchAndCacheFaviconAsync(new Uri(key)));
    }

    private static string GetCachePath(string url, string cacheDir)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(url));
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

        // This token is used for graceful shutdown and to cancel the HTML task if the ico task succeeds first.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        FetchResult icoResult = default;
        FetchResult htmlResult = default;
        bool fetchAttempted = false;

        try
        {
            var icoTask = FetchAndProcessUrlAsync(new Uri(url, "/favicon.ico"), linkedCts.Token);
            var htmlTask = FetchAndProcessHtmlAsync(url, linkedCts.Token);

            var tasks = new List<Task<FetchResult>> { icoTask, htmlTask };

            while (tasks.Any())
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);

                if (completedTask.IsCompletedSuccessfully && completedTask.Result.Size >= TargetIconSize)
                {
                    // A task finished with a good icon. We can cancel the other one.
                    linkedCts.Cancel();
                    fetchAttempted = true; // Mark as attempted, but potentially successful
                    break;
                }
            }

            // Await both tasks to collect their results, catching exceptions for tasks that were cancelled.
            try { icoResult = await icoTask; } catch (OperationCanceledException) { /* Expected if cancelled */ }
            try { htmlResult = await htmlTask; } catch (OperationCanceledException) { /* Expected if cancelled */ }

            var bestResult = SelectBestFavicon(icoResult, htmlResult);

            if (bestResult.TempPath != null)
            {
                File.Move(bestResult.TempPath, cachePath, true);
                _context.API.LogDebug(nameof(FaviconService), $"Favicon for {urlString} cached successfully.");
                _failedFetches.TryRemove(urlString, out _); // Remove from blacklist on success
                return cachePath;
            }

            _context.API.LogDebug(nameof(FaviconService), $"No suitable favicon found for {urlString} after all tasks.");
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconService), $"Error in favicon fetch for {urlString}", ex);
            fetchAttempted = true; // Mark as attempted and failed
        }
        finally
        {
            if (icoResult.TempPath != null && File.Exists(icoResult.TempPath)) File.Delete(icoResult.TempPath);
            if (htmlResult.TempPath != null && File.Exists(htmlResult.TempPath)) File.Delete(htmlResult.TempPath);
            _ongoingFetches.TryRemove(urlString, out _);

            // If fetch was attempted but no favicon found or an exception occurred, add to failed fetches
            if (fetchAttempted && !File.Exists(cachePath))
            {
                _failedFetches[urlString] = DateTime.UtcNow;
            }
        }

        return null;
    }

    private FetchResult SelectBestFavicon(FetchResult icoResult, FetchResult htmlResult)
    {
        if (htmlResult.Size >= TargetIconSize) return htmlResult;
        if (icoResult.Size >= TargetIconSize) return icoResult;
        if (htmlResult.Size > icoResult.Size) return htmlResult;
        // If sizes are equal, prefer ico as it's the standard. If htmlResult was better, it would likely have a larger size.
        if (icoResult.Size >= 0) return icoResult;
        if (htmlResult.Size >= 0) return htmlResult;
        return default;
    }

    private async Task<FetchResult> FetchAndProcessHtmlAsync(Uri pageUri, CancellationToken token)
    {
        var candidates = await GetCandidatesFromHtmlAsync(pageUri, token);

        foreach (var candidate in candidates)
        {
            if (Uri.TryCreate(candidate.Url, UriKind.Absolute, out var candidateUri))
            {
                var result = await FetchAndProcessUrlAsync(candidateUri, token);
                // If we got a valid image, return it immediately and don't try other candidates.
                if (result.TempPath != null)
                {
                    return result;
                }
            }
        }

        return default;
    }

    private async Task<FetchResult> FetchAndProcessUrlAsync(Uri faviconUri, CancellationToken token)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, faviconUri);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                File.Delete(tempPath);
                return default;
            }

            if (response.Content.Headers.ContentLength > MaxFaviconBytes)
            {
                File.Delete(tempPath);
                return default;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            await using var memoryStream = new MemoryStream();

            var buffer = new byte[8192];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                totalBytesRead += bytesRead;
                if (totalBytesRead > MaxFaviconBytes)
                {
                    File.Delete(tempPath);
                    return default;
                }
                await memoryStream.WriteAsync(buffer, 0, bytesRead, token);
            }

            memoryStream.Position = 0;
            var (pngData, size) = await ToPng(memoryStream, token);

            if (pngData is { Length: > 0 })
            {
                await File.WriteAllBytesAsync(tempPath, pngData, token);
                return new FetchResult(tempPath, size);
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.HostNotFound })
        {
            // Host not found is a common, expected error for misconfigured favicons. Don't log as a full error.
            _context.API.LogDebug(nameof(FaviconService), $"Favicon host not found for URI: {faviconUri}");
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            _context.API.LogWarn(nameof(FaviconService), $"HttpClient timed out for {faviconUri}.");
        }
        catch (OperationCanceledException)
        {
            // This is expected if another task cancels this one. No need to log.
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconService), $"Favicon fetch/process failed for {faviconUri}", ex);
        }

        File.Delete(tempPath);
        return default;
    }

    private async Task<List<FaviconCandidate>> GetCandidatesFromHtmlAsync(Uri pageUri, CancellationToken token)
    {
        try
        {
            using var response = await _httpClient.GetAsync(pageUri, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) return new List<FaviconCandidate>();

            var baseUri = response.RequestMessage?.RequestUri ?? pageUri;

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            var contentBuilder = new StringBuilder();
            var buffer = new char[4096];
            int charsRead;
            var totalCharsRead = 0;
            const int maxCharsToRead = 500 * 1024;

            while (!token.IsCancellationRequested && (charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0 && totalCharsRead < maxCharsToRead)
            {
                contentBuilder.Append(buffer, 0, charsRead);
                totalCharsRead += charsRead;

                if (contentBuilder.ToString().Contains("</head>", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            return ParseLinkTags(contentBuilder.ToString(), baseUri)
                    .OrderByDescending(c => c.Score)
                    .ToList();
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            _context.API.LogWarn(nameof(FaviconService), $"HttpClient timed out fetching HTML for {pageUri}.");
        }
        catch (OperationCanceledException)
        {
            // This is expected if another task cancels this one. No need to log.
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconService), $"Failed to fetch or parse HTML head for {pageUri}", ex);
        }
        return new List<FaviconCandidate>();
    }

    private List<FaviconCandidate> ParseLinkTags(string htmlContent, Uri originalBaseUri)
    {
        var candidates = new List<FaviconCandidate>();
        var effectiveBaseUri = originalBaseUri;

        var baseMatch = BaseHrefRegex().Match(htmlContent);
        if (baseMatch.Success)
        {
            var baseHref = baseMatch.Groups["v"].Value;
            if (Uri.TryCreate(originalBaseUri, baseHref, out var newBaseUri))
            {
                effectiveBaseUri = newBaseUri;
            }
        }

        foreach (Match linkMatch in LinkTagRegex().Matches(htmlContent))
        {
            var linkTag = linkMatch.Value;
            var relMatch = RelAttributeRegex().Match(linkTag);
            if (!relMatch.Success || !relMatch.Groups["v"].Value.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;

            var hrefMatch = HrefAttributeRegex().Match(linkTag);
            if (!hrefMatch.Success) continue;

            var href = hrefMatch.Groups["v"].Value;
            if (string.IsNullOrWhiteSpace(href)) continue;

            _context.API.LogDebug(nameof(ParseLinkTags), $"Found potential favicon link. Raw tag: '{linkTag}', Extracted href: '{href}', Base URI: '{effectiveBaseUri}'");

            if (href.StartsWith("//"))
            {
                href = effectiveBaseUri.Scheme + ":" + href;
            }

            if (!Uri.TryCreate(effectiveBaseUri, href, out var fullUrl))
            {
                _context.API.LogWarn(nameof(ParseLinkTags), $"Failed to create a valid URI from href: '{href}' and base URI: '{effectiveBaseUri}'");
                continue;
            }

            candidates.Add(new FaviconCandidate(fullUrl.ToString(), CalculateFaviconScore(linkTag, fullUrl.ToString())));
        }

        return candidates;
    }

    private static int CalculateFaviconScore(string linkTag, string fullUrl)
    {
        var extension = Path.GetExtension(fullUrl).ToUpperInvariant();
        if (extension == ".SVG") return 10000;

        var sizesMatch = SizesAttributeRegex().Match(linkTag);
        if (sizesMatch.Success)
        {
            var sizesValue = sizesMatch.Groups["v"].Value.ToUpperInvariant();
            if (sizesValue == "ANY") return 1000;

            var firstSizePart = sizesValue.Split(' ')[0];
            if (int.TryParse(firstSizePart.Split('X')[0], out var size))
            {
                return size;
            }
        }

        if (extension == ".ICO") return 32; // Default score for .ico is 32 as it's likely to contain that size

        return 16; // Default score for other bitmaps
    }

    private async Task<(byte[]? PngData, int Size)> ToPng(Stream stream, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, token);
        ms.Position = 0;

        // The returned 'Size' is the original width of the icon, used for scoring the best favicon.
        // It does not reflect the final dimensions of the resized PNG.

        // 1. Try to decode as SVG.
        var (pngData, size) = TryConvertSvgToPng(ms);
        if (pngData is not null) return (pngData, size);

        ms.Position = 0;
        // 2. Try to decode as an ICO file to correctly handle multiple frames.
        (pngData, size) = TryConvertIcoToPng(ms);
        if (pngData is not null) return (pngData, size);

        ms.Position = 0;
        // 3. Fallback for simple bitmaps or ICOs that IconBitmapDecoder failed on.
        (pngData, size) = TryConvertBitmapToPng(ms);
        return (pngData, size);
    }

    private static (byte[]? PngData, int Size) TryConvertSvgToPng(Stream stream)
    {
        try
        {
            using var svg = new SKSvg();
            if (svg.Load(stream) is not null && svg.Picture is not null)
            {
                using var bitmap = new SKBitmap(TargetIconSize, TargetIconSize);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);
                var scaleMatrix = SKMatrix.CreateScale((float)TargetIconSize / svg.Picture.CullRect.Width, (float)TargetIconSize / svg.Picture.CullRect.Height);
                canvas.DrawPicture(svg.Picture, in scaleMatrix);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                return (data.ToArray(), TargetIconSize);
            }
        }
        catch { /* Not a valid SVG, or SkiaSharp failed. */ }

        return (null, 0);
    }

    private static (byte[]? PngData, int Size) TryConvertIcoToPng(Stream stream)
    {
        try
        {
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Any())
            {
                var largestFrame = decoder.Frames.OrderByDescending(f => f.Width * f.Height).First();

                using var pngStream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(largestFrame));
                encoder.Save(pngStream);

                pngStream.Position = 0;
                using var original = SKBitmap.Decode(pngStream);
                if (original != null)
                {
                    var originalWidth = original.Width;
                    var info = new SKImageInfo(TargetIconSize, TargetIconSize, original.ColorType, original.AlphaType);
                    using var resized = original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
                    if (resized != null)
                    {
                        using var image = SKImage.FromBitmap(resized);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                        return (data.ToArray(), originalWidth);
                    }
                }
            }
        }
        catch { /* Not an ICO, or a format IconBitmapDecoder doesn't support. Fallback will try SKBitmap.Decode. */ }

        return (null, 0);
    }

    private (byte[]? PngData, int Size) TryConvertBitmapToPng(Stream stream)
    {
        try
        {
            using var original = SKBitmap.Decode(stream);
            if (original != null)
            {
                var originalWidth = original.Width;
                var info = new SKImageInfo(TargetIconSize, TargetIconSize, original.ColorType, original.AlphaType);
                using var resized = original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (resized != null)
                {
                    using var image = SKImage.FromBitmap(resized);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                    return (data.ToArray(), originalWidth);
                }
            }
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconService), "Failed to decode or convert bitmap with final fallback", ex);
        }

        return (null, 0);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _ongoingFetches.Clear();
        _failedFetches.Clear(); // Clear failed fetches on dispose
        _httpClient.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
