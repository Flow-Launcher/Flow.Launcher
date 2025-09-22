#nullable enable
using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public partial class FaviconService : IDisposable
{
    private readonly PluginInitContext _context;
    private readonly Settings _settings;
    private readonly string _faviconCacheDir;
    private readonly HttpClient _httpClient;
    private readonly LocalFaviconExtractor _localExtractor;

    private readonly ConcurrentDictionary<string, Task<string?>> _ongoingFetches = new();

    [GeneratedRegex("<link[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
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

public FaviconService(PluginInitContext context, Settings settings, string tempPath)
    {
        _context = context;
        _settings = settings;

        _faviconCacheDir = Path.Combine(context.CurrentPluginMetadata.PluginCacheDirectoryPath, "FaviconCache");
        Directory.CreateDirectory(_faviconCacheDir);
        
        _localExtractor = new LocalFaviconExtractor(context, tempPath);

        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
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
        return _ongoingFetches.GetOrAdd(authority, key => FetchAndCacheFaviconAsync(new Uri(key), token));
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

    private async Task<string?> FetchAndCacheFaviconAsync(Uri url, CancellationToken token)
    {
        var urlString = url.GetLeftPart(UriPartial.Authority);
        var cachePath = GetCachePath(urlString, _faviconCacheDir);
        if (File.Exists(cachePath)) return cachePath;

        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        overallCts.CancelAfter(TimeSpan.FromSeconds(10));
        var linkedToken = overallCts.Token;

        (string? TempPath, int Size) icoResult = (null, -1);
        (string? TempPath, int Size) htmlResult = (null, -1);

        try
        {
            var icoTask = FetchAndProcessUrlAsync(new Uri(url, "/favicon.ico"), linkedToken);
            var htmlTask = FetchAndProcessHtmlAsync(url, linkedToken);

            await Task.WhenAll(icoTask, htmlTask);

            icoResult = icoTask.Result;
            htmlResult = htmlTask.Result;

            string? winnerPath = null;
            if (htmlResult.Size >= 32)
                winnerPath = htmlResult.TempPath;
            else if (icoResult.Size >= 32)
                winnerPath = icoResult.TempPath;
            else if (htmlResult.Size > icoResult.Size)
                winnerPath = htmlResult.TempPath;
            else if (icoResult.Size >= 0)
                winnerPath = icoResult.TempPath;
            else if (htmlResult.Size >= 0)
                winnerPath = htmlResult.TempPath;

            if (winnerPath != null)
            {
                File.Move(winnerPath, cachePath, true);
                _context.API.LogDebug(nameof(FaviconService), $"Favicon for {urlString} cached successfully.");
                return cachePath;
            }
            
            _context.API.LogDebug(nameof(FaviconService), $"No suitable favicon found for {urlString} after all tasks.");
        }
        catch (OperationCanceledException) { /* Swallow cancellation */ }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconService), $"Error in favicon fetch for {urlString}", ex);
        }
        finally
        {
            if (icoResult.TempPath != null && File.Exists(icoResult.TempPath)) File.Delete(icoResult.TempPath);
            if (htmlResult.TempPath != null && File.Exists(htmlResult.TempPath)) File.Delete(htmlResult.TempPath);
            _ongoingFetches.TryRemove(urlString, out _);
        }

        return null;
    }
    
    private async Task<(string? TempPath, int Size)> FetchAndProcessHtmlAsync(Uri pageUri, CancellationToken token)
    {
        var bestCandidate = await GetBestCandidateFromHtmlAsync(pageUri, token);
        if (bestCandidate != null && Uri.TryCreate(bestCandidate.Value.Url, UriKind.Absolute, out var candidateUri))
        {
            return await FetchAndProcessUrlAsync(candidateUri, token);
        }
        return (null, -1);
    }

    private async Task<(string? TempPath, int Size)> FetchAndProcessUrlAsync(Uri faviconUri, CancellationToken token)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            _context.API.LogDebug(nameof(FaviconService), $"Attempting to fetch favicon: {faviconUri}");
            using var request = new HttpRequestMessage(HttpMethod.Get, faviconUri);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
            
            if (!response.IsSuccessStatusCode)
            {
                _context.API.LogDebug(nameof(FaviconService), $"Fetch failed for {faviconUri} with status code {response.StatusCode}");
                File.Delete(tempPath);
                return (null, -1);
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            var (pngData, size) = await ToPng(contentStream, token);
            
            if (pngData is { Length: > 0 })
            {
                await File.WriteAllBytesAsync(tempPath, pngData, token);
                _context.API.LogDebug(nameof(FaviconService), $"Successfully processed favicon for {faviconUri} with original size {size}x{size}");
                return (tempPath, size);
            }
            
            _context.API.LogDebug(nameof(FaviconService), $"Failed to process or invalid image for {faviconUri}.");
        }
        catch (OperationCanceledException) { _context.API.LogDebug(nameof(FaviconService), $"Favicon fetch cancelled for {faviconUri}."); }
        catch (Exception ex) when (ex is HttpRequestException or NotSupportedException)
        {
             _context.API.LogDebug(nameof(FaviconService), $"Favicon fetch/process failed for {faviconUri}: {ex.Message}");
        }
        
        File.Delete(tempPath);
        return (null, -1);
    }
    
    private async Task<FaviconCandidate?> GetBestCandidateFromHtmlAsync(Uri pageUri, CancellationToken token)
    {
        try
        {
            var response = await _httpClient.GetAsync(pageUri, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) return null;

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
                     .FirstOrDefault();
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _context.API.LogDebug(nameof(FaviconService), $"Failed to fetch or parse HTML head for {pageUri}: {ex.Message}");
        }
        return null;
    }
    
    private static List<FaviconCandidate> ParseLinkTags(string htmlContent, Uri originalBaseUri)
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

            if (href.StartsWith("//"))
            {
                href = effectiveBaseUri.Scheme + ":" + href;
            }

            if (!Uri.TryCreate(effectiveBaseUri, href, out var fullUrl)) continue;

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

        try
        {
            using var svg = new SKSvg();
            if (svg.Load(ms) is not null && svg.Picture is not null)
            {
                using var bitmap = new SKBitmap(32, 32);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);
                var scaleMatrix = SKMatrix.CreateScale(32 / svg.Picture.CullRect.Width, 32 / svg.Picture.CullRect.Height);
                canvas.DrawPicture(svg.Picture, in scaleMatrix);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                return (data.ToArray(), 32);
            }
        }
        catch { /* Not an SVG */ }

        ms.Position = 0;
        try
        {
            var decoder = new IconBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Any())
            {
                var largestFrame = decoder.Frames.OrderByDescending(f => f.Width * f.Height).First();
                
                await using var pngStream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(largestFrame));
                encoder.Save(pngStream);
                
                pngStream.Position = 0;
                using var original = SKBitmap.Decode(pngStream);
                if (original != null)
                {
                    var originalWidth = original.Width;
                    var info = new SKImageInfo(32, 32, original.ColorType, original.AlphaType);
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
        catch (Exception ex)
        {
            _context.API.LogDebug(nameof(FaviconService), $"Could not decode stream as ICO: {ex.Message}");
        }

        ms.Position = 0;
        try
        {
            using var original = SKBitmap.Decode(ms);
            if (original != null)
            {
                var originalWidth = original.Width;
                var info = new SKImageInfo(32, 32, original.ColorType, original.AlphaType);
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
            _context.API.LogException(nameof(FaviconService), "Failed to decode or convert bitmap", ex);
            return (null, 0);
        }

        return (null, 0);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
