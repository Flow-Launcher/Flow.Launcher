using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public record CommunityPluginSource(string ManifestFileUrl)
    {
        private static readonly string ClassName = nameof(CommunityPluginSource);

        private string latestEtag = "";

        private List<UserPlugin> plugins = [];

        private static readonly JsonSerializerOptions PluginStoreItemSerializationOption = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        /// <summary>
        /// Fetch and deserialize the contents of a plugins.json file found at <see cref="ManifestFileUrl"/>.
        /// We use conditional http requests to keep repeat requests fast.
        /// </summary>
        /// <remarks>
        /// This method will only return plugin details when the underlying http request is successful (200 or 304).
        /// In any other case, an exception is raised
        /// </remarks>
        public async Task<List<UserPlugin>> FetchAsync(CancellationToken token)
        {
            PublicApi.Instance.LogInfo(ClassName, $"Loading plugins from {ManifestFileUrl}");

            var request = new HttpRequestMessage(HttpMethod.Get, ManifestFileUrl);

            request.Headers.Add("If-None-Match", latestEtag);

            try
            {
                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    plugins = await response.Content
                        .ReadFromJsonAsync<List<UserPlugin>>(PluginStoreItemSerializationOption, cancellationToken: token)
                        .ConfigureAwait(false);
                    latestEtag = response.Headers.ETag?.Tag;

                    PublicApi.Instance.LogInfo(ClassName, $"Loaded {plugins.Count} plugins from {ManifestFileUrl}");
                    return plugins;
                }
                else if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    PublicApi.Instance.LogInfo(ClassName, $"Resource {ManifestFileUrl} has not been modified.");
                    return plugins;
                }
                else
                {
                    PublicApi.Instance.LogWarn(ClassName, $"Failed to load resource {ManifestFileUrl} with response {response.StatusCode}");
                    return null;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                PublicApi.Instance.LogDebug(ClassName, $"Fetching from {ManifestFileUrl} was cancelled by caller.");
                return null;
            }
            catch (TaskCanceledException)
            {
                // Likely an HttpClient timeout or external cancellation not requested by our token
                PublicApi.Instance.LogWarn(ClassName, $"Fetching from {ManifestFileUrl} timed out.");
                return null;
            }
            catch (Exception e)
            {
                if (e is HttpRequestException or WebException or SocketException || e.InnerException is TimeoutException)
                {
                    PublicApi.Instance.LogException(ClassName, $"Check your connection and proxy settings to {ManifestFileUrl}.", e);
                }
                else
                {
                    PublicApi.Instance.LogException(ClassName, "Error Occurred", e);
                }
                return null;
            }
        }
    }
}
