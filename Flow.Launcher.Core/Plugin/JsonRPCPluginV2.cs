using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin.JsonRPCV2Models;
using Flow.Launcher.Plugin;
using StreamJsonRpc;


namespace Flow.Launcher.Core.Plugin
{
    internal abstract class JsonRPCPluginV2 : JsonRPCPluginBase, IDisposable
    {
        public abstract string SupportedLanguage { get; set; }

        public const string JsonRpc = "JsonRPC";

        protected abstract JsonRpc RPC { get; set; }

        protected StreamReader ErrorStream { get; set; }


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

            _ = ReadErrorAsync();

            async Task ReadErrorAsync()
            {
                var error = await ErrorStream.ReadToEndAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }
            }
        }

        public void Dispose()
        {
            RPC?.Dispose();
            ErrorStream?.Dispose();
        }
    }
}
