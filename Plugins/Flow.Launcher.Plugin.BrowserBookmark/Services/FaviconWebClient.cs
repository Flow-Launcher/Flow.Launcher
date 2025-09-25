#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public class FaviconWebClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly PluginInitContext _context;
    private const int MaxFaviconBytes = 250 * 1024;

    public FaviconWebClient(PluginInitContext context)
    {
        _context = context;
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

    public async Task<(string? Html, Uri BaseUri)?> GetHtmlHeadAsync(Uri pageUri, CancellationToken token)
    {
        try
        {
            using var response = await _httpClient.GetAsync(pageUri, HttpCompletionOption.ResponseHeadersRead, token);
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
            return (contentBuilder.ToString(), baseUri);
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            _context.API.LogWarn(nameof(FaviconWebClient), $"HttpClient timed out fetching HTML for {pageUri}.");
        }
        catch (OperationCanceledException) { /* Expected if another task cancels this one */ }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconWebClient), $"Failed to fetch or parse HTML head for {pageUri}", ex);
        }
        return null;
    }

    public async Task<MemoryStream?> DownloadFaviconAsync(Uri faviconUri, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, faviconUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
                return null;
            
            if (response.Content.Headers.ContentLength > MaxFaviconBytes)
                return null;
            
            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            var memoryStream = new MemoryStream();

            var buffer = new byte[8192];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                totalBytesRead += bytesRead;
                if (totalBytesRead > MaxFaviconBytes)
                {
                    await memoryStream.DisposeAsync();
                    return null;
                }
                await memoryStream.WriteAsync(buffer, 0, bytesRead, token);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound })
        {
            _context.API.LogDebug(nameof(FaviconWebClient), $"Favicon host not found for URI: {faviconUri}");
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            _context.API.LogWarn(nameof(FaviconWebClient), $"HttpClient timed out for {faviconUri}.");
        }
        catch (OperationCanceledException) { /* Expected if another task cancels this one */ }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(FaviconWebClient), $"Favicon fetch failed for {faviconUri}", ex);
        }
        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
