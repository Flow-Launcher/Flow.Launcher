using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        private const string manifestFileUrl = "https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/plugin_api_v2/plugins.json";

        private static HttpClient httpClient = new HttpClient();

        private static readonly SemaphoreSlim manifestUpdateLock = new(1);

        private static string latestEtag = "";

        public static List<UserPlugin> UserPlugins { get; private set; } = new List<UserPlugin>();

        public static async Task UpdateManifestAsync()
        {
            try
            {
                await manifestUpdateLock.WaitAsync().ConfigureAwait(false);

                var request = new HttpRequestMessage(HttpMethod.Get, manifestFileUrl);
                request.Headers.Add("If-None-Match", latestEtag);

                var response = await httpClient.SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.Info($"|PluginsManifest.{nameof(UpdateManifestAsync)}|Fetched plugins from manifest repo");

                    var json = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    UserPlugins = await JsonSerializer.DeserializeAsync<List<UserPlugin>>(json).ConfigureAwait(false);

                    latestEtag = response.Headers.ETag.Tag;
                }
                else if (response.StatusCode != HttpStatusCode.NotModified)
                {
                    Log.Warn($"|PluginsManifest.{nameof(UpdateManifestAsync)}|Http response for manifest file was {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginsManifest.{nameof(UpdateManifestAsync)}|Http request failed", e);
            }
            finally
            {
                manifestUpdateLock.Release();
            }
        }
    }
}