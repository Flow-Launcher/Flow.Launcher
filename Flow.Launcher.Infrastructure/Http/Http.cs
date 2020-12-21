using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using System;

namespace Flow.Launcher.Infrastructure.Http
{
    public static class Http
    {
        private const string UserAgent = @"Mozilla/5.0 (Trident/7.0; rv:11.0) like Gecko";

        private static HttpClient client;
        private static SocketsHttpHandler socketsHttpHandler = new SocketsHttpHandler()
        {
            UseProxy = true,
            Proxy = WebProxy
        };

        static Http()
        {
            // need to be added so it would work on a win10 machine
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls
                                                    | SecurityProtocolType.Tls11
                                                    | SecurityProtocolType.Tls12;

            client = new HttpClient(socketsHttpHandler, false);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        }
        private static HttpProxy proxy;
        public static HttpProxy Proxy
        {
            private get
            {
                return proxy;
            }
            set
            {
                proxy = value;
                UpdateProxy();
            }
        }

        public static WebProxy WebProxy { get; private set; } = new WebProxy();

        /// <summary>
        /// Update the Address of the Proxy to modify the client Proxy
        /// </summary>
        public static void UpdateProxy()
        // TODO: need test with a proxy
        {
            if (Proxy != null && Proxy.Enabled && !string.IsNullOrEmpty(Proxy.Server))
            {
                if (string.IsNullOrEmpty(Proxy.UserName) || string.IsNullOrEmpty(Proxy.Password))
                {
                    WebProxy.Address = new Uri($"http://{Proxy.Server}:{Proxy.Port}");
                    WebProxy.Credentials = null;
                }
                else
                {
                    WebProxy.Address = new Uri($"http://{Proxy.Server}:{Proxy.Port}");
                    WebProxy.Credentials = new NetworkCredential(Proxy.UserName, Proxy.Password);
                }
            }
            else
            {
                WebProxy.Address = new WebProxy().Address;
                WebProxy.Credentials = null;
            }
        }

        public static async Task Download([NotNull] string url, [NotNull] string filePath)
        {
            using var response = await client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                await using var fileStream = new FileStream(filePath, FileMode.CreateNew);
                await response.Content.CopyToAsync(fileStream);
            }
            else
            {
                throw new WebException($"Error code <{response.StatusCode}> returned from <{url}>");
            }
        }

        public static async Task<string> Get([NotNull] string url, string encoding = "UTF-8")
        {
            Log.Debug($"|Http.Get|Url <{url}>");
            var response = await client.GetAsync(url);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.GetEncoding(encoding));
            var content = await reader.ReadToEndAsync();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return content;
            }
            else
            {
                throw new HttpRequestException($"Error code <{response.StatusCode}> with content <{content}> returned from <{url}>");
            }
        }
    }
}