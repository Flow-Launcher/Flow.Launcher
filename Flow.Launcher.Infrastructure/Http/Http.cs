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

        private static readonly WebProxy _proxy = new WebProxy();

        public static WebProxy WebProxy
        {
            get { return _proxy; }
        }

        /// <summary>
        /// Update the Address of the Proxy to modify the client Proxy
        /// </summary>
        public static void UpdateProxy(ProxyProperty property)
        {
            (_proxy.Address, _proxy.Credentials) = property switch
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
                ProxyProperty.Server => (new Uri($"http://{Proxy.Server}:{Proxy.Port}"), _proxy.Credentials),
                ProxyProperty.Port => (new Uri($"http://{Proxy.Server}:{Proxy.Port}"), _proxy.Credentials),
                ProxyProperty.UserName => (_proxy.Address, new NetworkCredential(Proxy.UserName, Proxy.Password)),
                ProxyProperty.Password => (_proxy.Address, new NetworkCredential(Proxy.UserName, Proxy.Password)),
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
                throw new HttpRequestException(
                    $"Error code <{response.StatusCode}> with content <{content}> returned from <{url}>");
            }
        }

        public static async Task<Stream> GetStreamAsync([NotNull] string url)
        {
            Log.Debug($"|Http.Get|Url <{url}>");
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
