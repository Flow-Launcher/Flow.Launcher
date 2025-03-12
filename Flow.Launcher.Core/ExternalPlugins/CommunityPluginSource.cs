using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public record CommunityPluginSource(string ManifestFileUrl)
    {
        private string latestEtag = "";

        private List<UserPlugin> plugins = new();

        private static JsonSerializerOptions PluginStoreItemSerializationOption = new JsonSerializerOptions()
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
            Log.Info(nameof(CommunityPluginSource), $"Loading plugins from {ManifestFileUrl}");

            var request = new HttpRequestMessage(HttpMethod.Get, ManifestFileUrl);

            request.Headers.Add("If-None-Match", latestEtag);

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                this.plugins = await response.Content
                    .ReadFromJsonAsync<List<UserPlugin>>(PluginStoreItemSerializationOption, cancellationToken: token)
                    .ConfigureAwait(false);
                this.latestEtag = response.Headers.ETag?.Tag;

                Log.Info(nameof(CommunityPluginSource), $"Loaded {this.plugins.Count} plugins from {ManifestFileUrl}");
                return this.plugins;
            }
            else if (response.StatusCode == HttpStatusCode.NotModified)
            {
                Log.Info(nameof(CommunityPluginSource), $"Resource {ManifestFileUrl} has not been modified.");
                return this.plugins;
            }
            else
            {
                Log.Warn(nameof(CommunityPluginSource),
                    $"Failed to load resource {ManifestFileUrl} with response {response.StatusCode}");
                throw new Exception($"Failed to load resource {ManifestFileUrl} with response {response.StatusCode}");
            }
        }
    }
}
