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
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public record CommunityPluginSource(string ManifestFileUrl)
    {
        private static readonly string ClassName = nameof(CommunityPluginSource);

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        private string latestEtag = "";

        private List<UserPlugin> plugins = new();

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
            API.LogInfo(ClassName, $"Loading plugins from {ManifestFileUrl}");

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

                    API.LogInfo(ClassName, $"Loaded {plugins.Count} plugins from {ManifestFileUrl}");
                    return plugins;
                }
                else if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    API.LogInfo(ClassName, $"Resource {ManifestFileUrl} has not been modified.");
                    return plugins;
                }
                else
                {
                    API.LogWarn(ClassName, $"Failed to load resource {ManifestFileUrl} with response {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception e)
            {
                if (e is HttpRequestException or WebException or SocketException || e.InnerException is TimeoutException)
                {
                    API.LogException(ClassName, $"Check your connection and proxy settings to {ManifestFileUrl}.", e);
                }
                else
                {
                    API.LogException(ClassName, "Error Occurred", e);
                }
                return null;
            }
        }
    }
}
