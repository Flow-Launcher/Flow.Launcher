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

    private record struct FaviconCandidate(string Url, int Score);

public FaviconService(PluginInitContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        _faviconCacheDir = Path.Combine(context.CurrentPluginMetadata.PluginCacheDirectoryPath, "FaviconCache");
        Directory.CreateDirectory(_faviconCacheDir);
        
        _localExtractor = new LocalFaviconExtractor(context);

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
                var pngData = await ToPng(new MemoryStream(localData), token);
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

        try
        {
            if (File.Exists(cachePath)) return cachePath;

            using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            overallCts.CancelAfter(TimeSpan.FromSeconds(10));
            
            if(await FetchAndProcessFavicon(new Uri(url, "/favicon.ico"), cachePath, overallCts.Token))
                return cachePath;

            var candidates = await GetFaviconCandidatesFromHtml(url, overallCts.Token);
            foreach (var candidate in candidates.OrderByDescending(c => c.Score).Select(c => c.Url).Distinct())
            {
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var candidateUri) && 
                    await FetchAndProcessFavicon(candidateUri, cachePath, overallCts.Token))
                    return cachePath;
            }
        }
        catch (OperationCanceledException) { /* Swallow */ }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconService), $"Error fetching favicon for {urlString}", ex);
        }
        finally
        {
            _ongoingFetches.TryRemove(urlString, out _);
        }

        return null;
    }
    
    private async Task<IEnumerable<FaviconCandidate>> GetFaviconCandidatesFromHtml(Uri pageUri, CancellationToken token)
    {
        var candidates = new List<FaviconCandidate>();
        try
        {
            var response = await _httpClient.GetAsync(pageUri, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) return candidates;

            var baseUri = response.RequestMessage?.RequestUri ?? pageUri;
            
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            
            var buffer = new char[20 * 1024];
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
            var content = new string(buffer, 0, charsRead);
            
            var headEndIndex = content.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (headEndIndex != -1) content = content[..headEndIndex];

            foreach (Match linkMatch in LinkTagRegex().Matches(content))
            {
                var linkTag = linkMatch.Value;
                var relMatch = RelAttributeRegex().Match(linkTag);
                if (!relMatch.Success || !relMatch.Groups["v"].Value.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;

                var hrefMatch = HrefAttributeRegex().Match(linkTag);
                if (!hrefMatch.Success) continue;
                
                var href = hrefMatch.Groups["v"].Value;
                if (string.IsNullOrWhiteSpace(href)) continue;

                if (!Uri.TryCreate(baseUri, href, out var fullUrl)) continue;

                candidates.Add(new FaviconCandidate(fullUrl.ToString(), CalculateFaviconScore(linkTag, fullUrl.ToString())));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _context.API.LogDebug(nameof(FaviconService), $"Failed to fetch or parse HTML head for {pageUri}: {ex.Message}");
        }
        return candidates;
    }
    
    private static int CalculateFaviconScore(string linkTag, string fullUrl)
    {
        var score = 0;
        var extension = Path.GetExtension(fullUrl).ToUpperInvariant();

        if (extension == ".SVG") score = 10000;
        else if (extension == ".ICO") score = 5000;
        else
        {
            var sizesMatch = SizesAttributeRegex().Match(linkTag);
            if (sizesMatch.Success)
            {
                var sizesValue = sizesMatch.Groups["v"].Value.ToUpperInvariant();
                if (sizesValue == "ANY") score = 100;
                else
                {
                    var firstSizePart = sizesValue.Split(' ')[0];
                    if (int.TryParse(firstSizePart.Split('X')[0], out var size)) score = size >= 32 ? 1000 - Math.Abs(size - 32) : size;
                }
            }
            else score = 32;
        }
        return score;
    }
    
private async Task<bool> FetchAndProcessFavicon(Uri faviconUri, string cachePath, CancellationToken token)
    {
        try
        {
            _context.API.LogDebug(nameof(FaviconService), $"Attempting to fetch favicon: {faviconUri}");
            using var request = new HttpRequestMessage(HttpMethod.Get, faviconUri);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
            if (!response.IsSuccessStatusCode) return false;

            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            var data = await ToPng(contentStream, token);
            
            if (data is { Length: > 0 })
            {
                await File.WriteAllBytesAsync(cachePath, data, token);
                return true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
             _context.API.LogDebug(nameof(FaviconService), $"Favicon fetch/process failed for {faviconUri}: {ex.Message}");
        }
        return false;
    }

private async Task<byte[]?> ToPng(Stream stream, CancellationToken token)
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
                _context.API.LogDebug(nameof(FaviconService), $"Decoded as SVG. Original size: {svg.Picture.CullRect.Width}x{svg.Picture.CullRect.Height}. Resizing to 32x32.");
                using var bitmap = new SKBitmap(32, 32);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);
                var scaleMatrix = SKMatrix.CreateScale(32 / svg.Picture.CullRect.Width, 32 / svg.Picture.CullRect.Height);
                canvas.DrawPicture(svg.Picture, ref scaleMatrix);
                
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                return data.ToArray();
            }
        }
        catch { /* Not an SVG */ }

        try
        {
            ms.Position = 0;
            using var original = SKBitmap.Decode(ms);
            if (original == null) return null;
            
            _context.API.LogDebug(nameof(FaviconService), $"Decoded as bitmap. Original size: {original.Width}x{original.Height}. Resizing to 32x32.");
            
            var info = new SKImageInfo(32, 32, original.ColorType, original.AlphaType);
            using var resized = original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized == null) return null;

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 80);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            _context.API.LogDebug(nameof(FaviconService), $"Failed to decode or convert bitmap: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
