using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin.JsonRPCV2Models;
using Flow.Launcher.Plugin;
using StreamJsonRpc;


namespace Flow.Launcher.Core.Plugin
{
    internal abstract class JsonRPCPluginV2 : JsonRPCPluginBase, IAsyncDisposable, IAsyncReloadable, IResultUpdated
    {
        public abstract string SupportedLanguage { get; set; }

        public const string JsonRpc = "JsonRPC";

        protected abstract IDuplexPipe ClientPipe { get; set; }

        protected StreamReader ErrorStream { get; set; }

        private JsonRpc RPC { get; set; }


        protected override async Task<bool> ExecuteResultAsync(JsonRPCResult result)
        {
            try
            {
                var res = await RPC.InvokeAsync<JsonRPCExecuteResponse>(result.JsonRPCAction.Method,
                    argument: result.JsonRPCAction.Parameters);

                return res.Hide;
            }
            catch
            {
                return false;
            }
        }

        public override async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            try
            {
                var res = await RPC.InvokeWithCancellationAsync<JsonRPCQueryResponseModel>("query",
                    new[] { query },
                    token);

                var results = ParseResults(res);

                return results;
            }
            catch
            {
                return new List<Result>();
            }
        }


        public override async Task InitAsync(PluginInitContext context)
        {
            await base.InitAsync(context);

            SetupJsonRPC();

            _ = ReadErrorAsync();

            await RPC.InvokeAsync("initialize", context);

            async Task ReadErrorAsync()
            {
                var error = await ErrorStream.ReadToEndAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }
            }
        }

        public event ResultUpdatedEventHandler ResultsUpdated;


        private void SetupJsonRPC()
        {
            var formatter = new JsonMessageFormatter();
            var handler = new NewLineDelimitedMessageHandler(ClientPipe,
                formatter);

            RPC = new JsonRpc(handler, new JsonRPCPublicAPI(Context.API));

            RPC.AddLocalRpcMethod("UpdateResults", new Action<string, JsonRPCQueryResponseModel>((rawQuery, response) =>
            {
                var results = ParseResults(response);
                ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs { Query = new Query()
                {
                    RawQuery = rawQuery
                }, Results = results });
            }));
            RPC.SynchronizationContext = null;
            RPC.StartListening();
        }

        public virtual Task ReloadDataAsync()
        {
            SetupJsonRPC();
            return Task.CompletedTask;
        }

        public virtual ValueTask DisposeAsync()
        {
            RPC?.Dispose();
            ErrorStream?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
