using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.ComponentModel;

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
            private get { return proxy; }
            set
            {
                proxy = value;
                proxy.PropertyChanged += UpdateProxy;
            }
        }

        public static WebProxy WebProxy { get; } = new WebProxy();

        /// <summary>
        /// Update the Address of the Proxy to modify the client Proxy
        /// </summary>
        public static void UpdateProxy(ProxyProperty property)
        {
            (WebProxy.Address, WebProxy.Credentials) = property switch
            {
                ProxyProperty.Enabled => Proxy.Enabled switch
                {
                    true => Proxy.UserName switch
                    {
                        var userName when !string.IsNullOrEmpty(userName) =>
                            (new Uri($"http://{Proxy.Server}:{Proxy.Port}"), null),
                        _ => (new Uri($"http://{Proxy.Server}:{Proxy.Port}"),
                            new NetworkCredential(Proxy.UserName, Proxy.Password))
                    },
                    false => (null, null)
                },
                ProxyProperty.Server => (new Uri($"http://{Proxy.Server}:{Proxy.Port}"), WebProxy.Credentials),
                ProxyProperty.Port => (new Uri($"http://{Proxy.Server}:{Proxy.Port}"), WebProxy.Credentials),
                ProxyProperty.UserName => (WebProxy.Address, new NetworkCredential(Proxy.UserName, Proxy.Password)),
                ProxyProperty.Password => (WebProxy.Address, new NetworkCredential(Proxy.UserName, Proxy.Password)),
                _ => throw new ArgumentOutOfRangeException()
            };
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
                throw new HttpRequestException($"Error code <{response.StatusCode}> returned from <{url}>");
            }
        }

        /// <summary>
        /// Asynchrously get the result as string from url.
        /// When supposing the result is long and large, try using GetStreamAsync to avoid reading as string
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static Task<string> GetAsync([NotNull] string url)
        {
            Log.Debug($"|Http.Get|Url <{url}>");
            return GetAsync(new Uri(url.Replace("#", "%23")));
        }

        public static async Task<string> GetAsync([NotNull] Uri url)
        {
            Log.Debug($"|Http.Get|Url <{url}>");
            using var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return content;
            }
            else
            {
                throw new HttpRequestException(
                    $"Error code <{response.StatusCode}> with content <{content}> returned from <{url}>");
            }
        }

        /// <summary>
        /// Asynchrously get the result as stream from url.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<Stream> GetStreamAsync([NotNull] string url)
        {
            Log.Debug($"|Http.Get|Url <{url}>");
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
