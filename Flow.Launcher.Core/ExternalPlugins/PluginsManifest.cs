using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastCache;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        private const string ManifestFileUrl =
            "https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/plugin_api_v2/plugins.json";

        private static string _latestEtag = "";


        public static async ValueTask<List<UserPlugin>> RetrieveManifestAsync()
        {
            if (Cached<List<UserPlugin>>.TryGet(0, out var cached))
            {
                return cached;
            }

            var result = await UpdateManifestAsync();

            return result;
        }

        public static async Task<List<UserPlugin>> UpdateManifestAsync(CancellationToken token = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, ManifestFileUrl);
            request.Headers.Add("If-None-Match", _latestEtag);

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Log.Info($"|PluginsManifest.{nameof(UpdateManifestAsync)}|Fetched plugins from manifest repo");

                await using var json = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

                var plugins = await JsonSerializer
                    .DeserializeAsync<List<UserPlugin>>(json, cancellationToken: token).ConfigureAwait(false);

                _latestEtag = response.Headers.ETag!.Tag;
                if (plugins.Any())
                    Cached<List<UserPlugin>>.Save(0, plugins, TimeSpan.FromHours(6));
                return plugins;
            }

            if (response.StatusCode != HttpStatusCode.NotModified)
            {
                Log.Warn(
                    $"|PluginsManifest.{nameof(UpdateManifestAsync)}|Http response for manifest file was {response.StatusCode}");
            }

            return new List<UserPlugin>();
        }
    }
}
