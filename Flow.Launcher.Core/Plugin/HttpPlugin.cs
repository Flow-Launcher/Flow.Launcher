using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    internal class HttpPlugin : JsonRPCPlugin
    {
        private readonly HttpClient client;
        private readonly string url;

        public override string SupportedLanguage { get; set; } = AllowedLanguage.Http;

        public HttpPlugin(string url)
        {
            this.client = new HttpClient();
            this.url = url;
        }

        protected override string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Content = new StringContent(rpcRequest.ToString(), Encoding.UTF8);
            var response = this.client.Send(requestMessage, token);
            return response.Content.ReadAsStringAsync().Result;
        }

        protected override async Task<Stream> RequestAsync(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            var response = await this.client.PostAsync(url, new StringContent(rpcRequest.ToString(), Encoding.UTF8), token).ConfigureAwait(false);
            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }
    }
}
